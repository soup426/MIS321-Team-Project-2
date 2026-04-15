using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Data;
using MulhollandRealEstate.API.Models;
using MulhollandRealEstate.API.Services;

namespace MulhollandRealEstate.API.Controllers;

[ApiController]
[Route("api/resident")]
public class ResidentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITriageService _triage;
    private readonly IConfiguration _configuration;

    public ResidentController(AppDbContext db, ITriageService triage, IConfiguration configuration)
    {
        _db = db;
        _triage = triage;
        _configuration = configuration;
    }

    public sealed class ResidentSubmitDto
    {
        public string PropertyId { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string? BuildingType { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhoneOrEmail { get; set; }
        public string RequestText { get; set; } = "";
    }

    [HttpPost("tickets")]
    [RequestSizeLimit(15_000_000)] // 15MB total
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> Submit([FromForm] ResidentSubmitDto dto, [FromForm] List<IFormFile>? images, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.PropertyId) || string.IsNullOrWhiteSpace(dto.UnitNumber) || string.IsNullOrWhiteSpace(dto.RequestText))
            return BadRequest(new { message = "propertyId, unitNumber, and requestText are required." });

        // Allocate a requestNumber (same approach as TicketsController).
        MaintenanceRequest ticket = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000_000);
            candidate += attempt;
            if (candidate < 1001) candidate += 1001;

            ticket = new MaintenanceRequest
            {
                RequestNumber = candidate,
                PropertyId = dto.PropertyId.Trim(),
                UnitNumber = dto.UnitNumber.Trim(),
                BuildingType = (dto.BuildingType ?? "").Trim(),
                TenantTenureMonths = 0,
                SubmissionChannel = "Resident",
                RequestTimestamp = DateTime.UtcNow,
                RequestText = dto.RequestText.Trim(),
                HasImage = false,
                ImageType = "None",
                ImageSeverityHint = "Unknown",
                PriorRequestsLast6Mo = 0,
                ActualCategory = "",
                ActualUrgency = ""
            };

            _db.MaintenanceRequests.Add(ticket);
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException sqlEx &&
                                              sqlEx.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry)
            {
                _db.ChangeTracker.Clear();
                if (attempt == 4) throw;
            }
        }

        if (ticket.Id <= 0)
            return StatusCode(503, new { message = "Could not allocate a unique requestNumber. Try again." });

        // Upload images (optional)
        var saved = 0;
        if (images is { Count: > 0 })
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
            var uploadsPath = _configuration["Uploads:Path"]
                              ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads"));
            var folder = Path.Combine(uploadsPath, ticket.RequestNumber.ToString());
            Directory.CreateDirectory(folder);

            foreach (var file in images)
            {
                if (file.Length <= 0) continue;
                if (!allowed.Contains(file.ContentType)) continue;

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                        : file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
                        : ".jpg";

                var safeExt = ext.Length <= 10 ? ext : ".img";
                var name = $"{Guid.NewGuid():N}{safeExt}";
                var fullPath = Path.Combine(folder, name);
                await using (var fs = System.IO.File.Create(fullPath))
                    await file.CopyToAsync(fs, ct);

                var storageKey = $"{ticket.RequestNumber}/{name}".Replace('\\', '/');
                _db.MaintenanceRequestImages.Add(new MaintenanceRequestImage
                {
                    MaintenanceRequestId = ticket.Id,
                    StorageKey = storageKey,
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    CreatedAt = DateTime.UtcNow
                });
                saved++;
            }

            if (saved > 0)
            {
                ticket.HasImage = true;
                ticket.ImageType = "Upload";
                ticket.ImageUrlOrCount = saved.ToString();
            }
        }

        // Attach a note event with contact info (optional)
        if (!string.IsNullOrWhiteSpace(dto.ContactName) || !string.IsNullOrWhiteSpace(dto.ContactPhoneOrEmail))
        {
            _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
            {
                MaintenanceRequestId = ticket.Id,
                EventType = "note",
                Actor = "resident",
                EventTimestamp = DateTime.UtcNow,
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    note = $"Contact: {dto.ContactName?.Trim()} {dto.ContactPhoneOrEmail?.Trim()}".Trim()
                })
            });
        }

        await _db.SaveChangesAsync(ct);

        // Auto-triage
        var result = await _triage.TriageAsync(ticket, ct);
        // reuse mapping logic from TicketsController implicitly via fields
        var threshold = _configuration.GetValue("Triage:HumanReviewBelowConfidence", 0.55m);
        ticket.PredictedCategory = result.PredictedCategory;
        ticket.PredictedUrgency = result.PredictedUrgency;
        ticket.ConfidenceScore = (decimal)result.Confidence;
        ticket.TagsJson = System.Text.Json.JsonSerializer.Serialize(result.Tags);
        ticket.RiskNotes = result.RiskNotes;
        ticket.TriageSource = result.Source;
        ticket.LastTriagedAt = DateTime.UtcNow;
        ticket.NeedsHumanReview = ticket.ConfidenceScore < threshold;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            requestNumber = ticket.RequestNumber,
            imagesSaved = saved
        });
    }
}

