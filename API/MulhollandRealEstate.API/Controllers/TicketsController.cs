using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Data;
using MulhollandRealEstate.API.Models;
using MulhollandRealEstate.API.Services;

namespace MulhollandRealEstate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITriageService _triage;
    private readonly IConfiguration _configuration;

    public TicketsController(AppDbContext db, ITriageService triage, IConfiguration configuration)
    {
        _db = db;
        _triage = triage;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> GetTickets(
        [FromQuery] string? urgency,
        [FromQuery] bool? needsHumanOnly,
        CancellationToken ct)
    {
        var q = _db.MaintenanceRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(urgency))
            q = q.Where(t => (t.PredictedUrgency ?? t.ActualUrgency) == urgency);
        if (needsHumanOnly == true)
            q = q.Where(t => t.NeedsHumanReview);

        var list = await q.OrderByDescending(t => t.RequestTimestamp).ToListAsync(ct);
        return Ok(list.Select(MapToDto).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TriageSummaryDto>> Summary(CancellationToken ct)
    {
        var rows = await _db.MaintenanceRequests.AsNoTracking().ToListAsync(ct);
        var triaged = rows.Where(r => r.LastTriagedAt != null).ToList();
        var byU = triaged
            .Where(r => !string.IsNullOrEmpty(r.PredictedUrgency))
            .GroupBy(r => r.PredictedUrgency!)
            .ToDictionary(g => g.Key, g => g.Count());

        var compared = triaged.Where(r => r.PredictedCategory != null && r.PredictedUrgency != null).ToList();
        var catMatch = compared.Count(r =>
            string.Equals(r.PredictedCategory, r.ActualCategory, StringComparison.OrdinalIgnoreCase));
        var urgMatch = compared.Count(r =>
            string.Equals(r.PredictedUrgency, r.ActualUrgency, StringComparison.OrdinalIgnoreCase));

        return Ok(new TriageSummaryDto
        {
            Total = rows.Count,
            Triaged = triaged.Count,
            NeedsHumanReview = triaged.Count(r => r.NeedsHumanReview),
            ByPredictedUrgency = byU,
            CategoryMatches = catMatch,
            UrgencyMatches = urgMatch,
            ComparedRows = compared.Count
        });
    }

    [HttpGet("{requestNumber:int}")]
    public async Task<ActionResult<TicketListItemDto>> GetOne(int requestNumber, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();
        return Ok(MapToDto(t));
    }

    [HttpPost("{requestNumber:int}/triage")]
    public async Task<ActionResult<TicketListItemDto>> TriageOne(int requestNumber, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        var result = await _triage.TriageAsync(t, ct);
        ApplyTriage(t, result);
        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(t));
    }

    [HttpPost("triage-all")]
    public async Task<ActionResult<object>> TriageAll(CancellationToken ct)
    {
        var rows = await _db.MaintenanceRequests.ToListAsync(ct);
        var n = 0;
        foreach (var t in rows)
        {
            var result = await _triage.TriageAsync(t, ct);
            ApplyTriage(t, result);
            n++;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { updated = n });
    }

    private void ApplyTriage(MaintenanceRequest t, TriageResult result)
    {
        var threshold = _configuration.GetValue("Triage:HumanReviewBelowConfidence", 0.55m);
        t.PredictedCategory = result.PredictedCategory;
        t.PredictedUrgency = result.PredictedUrgency;
        t.ConfidenceScore = (decimal)result.Confidence;
        t.TagsJson = JsonSerializer.Serialize(result.Tags);
        t.RiskNotes = result.RiskNotes;
        t.TriageSource = result.Source;
        t.LastTriagedAt = DateTime.UtcNow;
        t.NeedsHumanReview = t.ConfidenceScore < threshold;
    }

    private static TicketListItemDto MapToDto(MaintenanceRequest t)
    {
        string[] tags = [];
        if (!string.IsNullOrWhiteSpace(t.TagsJson))
        {
            try
            {
                tags = JsonSerializer.Deserialize<string[]>(t.TagsJson) ?? [];
            }
            catch
            {
                tags = t.TagsJson.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
        }

        bool? catMatch = null;
        bool? urgMatch = null;
        if (t.PredictedCategory != null && t.PredictedUrgency != null)
        {
            catMatch = string.Equals(t.PredictedCategory, t.ActualCategory, StringComparison.OrdinalIgnoreCase);
            urgMatch = string.Equals(t.PredictedUrgency, t.ActualUrgency, StringComparison.OrdinalIgnoreCase);
        }

        return new TicketListItemDto
        {
            Id = t.Id,
            RequestNumber = t.RequestNumber,
            PropertyId = t.PropertyId,
            UnitNumber = t.UnitNumber,
            BuildingType = t.BuildingType,
            RequestTimestamp = t.RequestTimestamp,
            RequestText = t.RequestText,
            HasImage = t.HasImage,
            ActualCategory = t.ActualCategory,
            ActualUrgency = t.ActualUrgency,
            PredictedCategory = t.PredictedCategory,
            PredictedUrgency = t.PredictedUrgency,
            ConfidenceScore = t.ConfidenceScore,
            Tags = tags,
            RiskNotes = t.RiskNotes,
            NeedsHumanReview = t.NeedsHumanReview,
            LastTriagedAt = t.LastTriagedAt,
            TriageSource = t.TriageSource,
            CategoryMatchesSample = catMatch,
            UrgencyMatchesSample = urgMatch
        };
    }
}
