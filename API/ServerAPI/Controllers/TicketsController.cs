using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
        [FromQuery] string? status,
        [FromQuery] long? assignedEmployeeId,
        CancellationToken ct)
    {
        var isManager = User.IsInRole("Manager") || User.IsInRole("Dispatcher");
        long? me = null;
        if (!isManager)
        {
            var idStr = User.FindFirstValue("employeeId");
            if (long.TryParse(idStr, out var id)) me = id;
        }

        var q = _db.MaintenanceRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(urgency))
            q = q.Where(t => (t.PredictedUrgency ?? t.ActualUrgency) == urgency);
        if (needsHumanOnly == true)
            q = q.Where(t => t.NeedsHumanReview);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);
        if (assignedEmployeeId.HasValue)
        {
            if (assignedEmployeeId.Value == 0)
                q = q.Where(t => t.AssignedEmployeeId == null);
            else
                q = q.Where(t => t.AssignedEmployeeId == assignedEmployeeId.Value);
        }
        else if (me.HasValue)
        {
            q = q.Where(t => t.AssignedEmployeeId == me.Value);
        }

        var list = await q.OrderByDescending(t => t.RequestTimestamp).ToListAsync(ct);
        var empNames = await _db.Employees.AsNoTracking()
            .Where(e => e.Active)
            .ToDictionaryAsync(e => e.Id, e => e.FullName, ct);

        return Ok(list.Select(t => MapToDto(t, t.AssignedEmployeeId is { } id && empNames.TryGetValue(id, out var name) ? name : null)).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TriageSummaryDto>> Summary(CancellationToken ct)
    {
        var isManager = User.IsInRole("Manager") || User.IsInRole("Dispatcher");
        long? me = null;
        if (!isManager)
        {
            var idStr = User.FindFirstValue("employeeId");
            if (long.TryParse(idStr, out var id)) me = id;
        }

        var rows = await _db.MaintenanceRequests.AsNoTracking().ToListAsync(ct);
        if (me.HasValue) rows = rows.Where(r => r.AssignedEmployeeId == me.Value).ToList();
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
        string? empName = null;
        if (t.AssignedEmployeeId is { } eid)
            empName = await _db.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync(ct);
        return Ok(MapToDto(t, empName));
    }

    public sealed class TicketImageDto
    {
        public long Id { get; set; }
        public int RequestNumber { get; set; }
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [HttpGet("{requestNumber:int}/images")]
    public async Task<ActionResult<IReadOnlyList<TicketImageDto>>> ListImages(int requestNumber, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        var list = await _db.MaintenanceRequestImages.AsNoTracking()
            .Where(x => x.MaintenanceRequestId == t.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var baseUrl = _configuration["Uploads:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        baseUrl = baseUrl.TrimEnd('/');

        return Ok(list.Select(i => new TicketImageDto
        {
            Id = i.Id,
            RequestNumber = requestNumber,
            Url = $"{baseUrl}/uploads/{i.StorageKey.TrimStart('/')}",
            FileName = i.FileName,
            ContentType = i.ContentType,
            SizeBytes = i.SizeBytes,
            CreatedAt = i.CreatedAt
        }).ToList());
    }

    [HttpPost("{requestNumber:int}/images")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<ActionResult<TicketImageDto>> UploadImage(int requestNumber, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "file is required" });

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { message = "Only jpeg, png, webp supported" });

        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        // Must match Program.cs static files configuration (defaults to ContentRoot/uploads).
        var uploadsPath = _configuration["Uploads:Path"]
                          ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads"));
        var folder = Path.Combine(uploadsPath, requestNumber.ToString());
        Directory.CreateDirectory(folder);

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

        var storageKey = $"{requestNumber}/{name}".Replace('\\', '/');

        var img = new MaintenanceRequestImage
        {
            MaintenanceRequestId = t.Id,
            StorageKey = storageKey,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };
        _db.MaintenanceRequestImages.Add(img);

        // Keep legacy fields coherent for the existing UI/seed schema.
        t.HasImage = true;
        t.ImageUrlOrCount = null; // will be filled with a count string below

        await _db.SaveChangesAsync(ct);

        var count = await _db.MaintenanceRequestImages.AsNoTracking()
            .CountAsync(x => x.MaintenanceRequestId == t.Id, ct);
        t.ImageUrlOrCount = count.ToString();
        await _db.SaveChangesAsync(ct);

        var baseUrl = _configuration["Uploads:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        baseUrl = baseUrl.TrimEnd('/');

        return Ok(new TicketImageDto
        {
            Id = img.Id,
            RequestNumber = requestNumber,
            Url = $"{baseUrl}/uploads/{storageKey}",
            FileName = img.FileName,
            ContentType = img.ContentType,
            SizeBytes = img.SizeBytes,
            CreatedAt = img.CreatedAt
        });
    }

    public sealed class SubmitTicketDto
    {
        public string PropertyId { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string BuildingType { get; set; } = "";
        public int TenantTenureMonths { get; set; }
        public string SubmissionChannel { get; set; } = "Portal";
        public DateTime? RequestTimestamp { get; set; }
        public string RequestText { get; set; } = "";
        public bool HasImage { get; set; }
        public string ImageType { get; set; } = "None";
        public string ImageSeverityHint { get; set; } = "Unknown";
        public string? ImageUrlOrCount { get; set; }
        public int PriorRequestsLast6Mo { get; set; }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<TicketListItemDto>> Submit([FromBody] SubmitTicketDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.PropertyId) || string.IsNullOrWhiteSpace(dto.UnitNumber) || string.IsNullOrWhiteSpace(dto.RequestText))
            return BadRequest(new { message = "propertyId, unitNumber, and requestText are required." });

        // Don't use MAX()+1 (racey and can be stale with mixed writers).
        // Use a time-based number and retry on duplicate key.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000_000);
            candidate += attempt; // tiny bump if collisions happen within the same ms
            if (candidate < 1001) candidate += 1001;

            var ticket = new MaintenanceRequest
            {
                RequestNumber = candidate,
                PropertyId = dto.PropertyId.Trim(),
                UnitNumber = dto.UnitNumber.Trim(),
                BuildingType = dto.BuildingType?.Trim() ?? "",
                TenantTenureMonths = dto.TenantTenureMonths,
                SubmissionChannel = string.IsNullOrWhiteSpace(dto.SubmissionChannel) ? "Portal" : dto.SubmissionChannel.Trim(),
                RequestTimestamp = dto.RequestTimestamp ?? DateTime.UtcNow,
                RequestText = dto.RequestText.Trim(),
                HasImage = dto.HasImage,
                ImageType = string.IsNullOrWhiteSpace(dto.ImageType) ? "None" : dto.ImageType.Trim(),
                ImageSeverityHint = string.IsNullOrWhiteSpace(dto.ImageSeverityHint) ? "Unknown" : dto.ImageSeverityHint.Trim(),
                ImageUrlOrCount = dto.ImageUrlOrCount,
                PriorRequestsLast6Mo = dto.PriorRequestsLast6Mo,
                // Not user input; keep empty for real submissions.
                ActualCategory = "",
                ActualUrgency = ""
            };

            _db.MaintenanceRequests.Add(ticket);
            try
            {
                await _db.SaveChangesAsync(ct);
                // Auto-triage on submit so the dashboard is filled immediately.
                var result = await _triage.TriageAsync(ticket, ct);
                ApplyTriage(ticket, result);
                await TryAutoAssignAsync(ticket, ct);
                await _db.SaveChangesAsync(ct);

                return CreatedAtAction(nameof(GetOne), new { requestNumber = ticket.RequestNumber }, MapToDto(ticket, null));
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException sqlEx && sqlEx.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry)
            {
                _db.ChangeTracker.Clear();
                continue;
            }
        }

        return StatusCode(503, new { message = "Could not allocate a unique requestNumber. Try again." });
    }

    [HttpPost("{requestNumber:int}/triage")]
    public async Task<ActionResult<TicketListItemDto>> TriageOne(int requestNumber, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        var result = await _triage.TriageAsync(t, ct);
        ApplyTriage(t, result);
        await TryAutoAssignAsync(t, ct);
        await _db.SaveChangesAsync(ct);
        string? empName = null;
        if (t.AssignedEmployeeId is { } eid)
            empName = await _db.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync(ct);
        return Ok(MapToDto(t, empName));
    }

    [HttpPost("triage-all")]
    public async Task<ActionResult<object>> TriageAll(CancellationToken ct)
    {
        if (!(User.IsInRole("Manager") || User.IsInRole("Dispatcher")))
            return Forbid();
        var rows = await _db.MaintenanceRequests.ToListAsync(ct);
        var n = 0;
        foreach (var t in rows)
        {
            var result = await _triage.TriageAsync(t, ct);
            ApplyTriage(t, result);
            await TryAutoAssignAsync(t, ct);
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

    private async Task TryAutoAssignAsync(MaintenanceRequest t, CancellationToken ct)
    {
        var enabled = _configuration.GetValue("AutoAssign:Enabled", true);
        if (!enabled) return;
        if (t.AssignedEmployeeId != null) return;
        if (t.NeedsHumanReview) return;

        var skillCode = string.IsNullOrWhiteSpace(t.PredictedCategory) ? null : t.PredictedCategory.Trim();
        if (string.IsNullOrWhiteSpace(skillCode)) return;

        var skill = await _db.Skills.AsNoTracking()
            .Where(s => s.SkillCode == skillCode)
            .Select(s => new { s.Id, s.SkillCode })
            .FirstOrDefaultAsync(ct);
        if (skill is null) return;

        // Find qualified employees and compute current open workload.
        var qualified = await _db.EmployeeSkills.AsNoTracking()
            .Where(es => es.SkillId == skill.Id)
            .Select(es => es.EmployeeId)
            .ToListAsync(ct);
        if (qualified.Count == 0) return;

        var employees = await _db.Employees.AsNoTracking()
            .Where(e => e.Active && qualified.Contains(e.Id))
            .Select(e => new { e.Id, e.FullName, e.MaxOpenTickets })
            .ToListAsync(ct);
        if (employees.Count == 0) return;

        var openCounts = await _db.MaintenanceRequests.AsNoTracking()
            .Where(r => r.Status == "Open" && r.AssignedEmployeeId != null && qualified.Contains(r.AssignedEmployeeId.Value))
            .GroupBy(r => r.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, ct);

        var best = employees
            .Select(e => new { e.Id, e.FullName, e.MaxOpenTickets, Open = openCounts.TryGetValue(e.Id, out var c) ? c : 0 })
            .Where(x => x.Open < x.MaxOpenTickets)
            .OrderBy(x => x.Open)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (best is null) return;

        t.AssignedEmployeeId = best.Id;
        t.AssignedAt = DateTime.UtcNow;
        t.AssignmentSource = "ai";

        _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
        {
            MaintenanceRequestId = t.Id,
            EventType = "assign",
            Actor = "auto-assign",
            DetailsJson = JsonSerializer.Serialize(new { message = $"Auto-assigned to {best.FullName} (#{best.Id}) via skill {skill.SkillCode}" }),
            EventTimestamp = DateTime.UtcNow
        });
    }

    [HttpGet("/api/employees")]
    [Authorize(Roles = "Manager,Dispatcher")]
    public async Task<ActionResult<IReadOnlyList<Employee>>> GetEmployees(CancellationToken ct)
    {
        var list = await _db.Employees.AsNoTracking()
            .Where(e => e.Active)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct);
        return Ok(list);
    }

    public sealed class CreateEmployeeDto
    {
        public string FullName { get; set; } = "";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string Role { get; set; } = "Maintenance";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? HomePropertyId { get; set; }
        public int MaxOpenTickets { get; set; } = 10;
    }

    [HttpPost("/api/employees")]
    [Authorize(Roles = "Manager,Dispatcher")]
    public async Task<ActionResult<Employee>> CreateEmployee([FromBody] CreateEmployeeDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new { message = "fullName is required" });

        var username = string.IsNullOrWhiteSpace(dto.Username)
            ? dto.FullName.Trim().Replace(" ", "").ToLowerInvariant()
            : dto.Username.Trim();
        if (await _db.Employees.AnyAsync(x => x.Username == username, ct))
            return Conflict(new { message = "username already exists" });

        var e = new Employee
        {
            FullName = dto.FullName.Trim(),
            Username = username,
            PasswordHash = PasswordHashing.Hash(string.IsNullOrWhiteSpace(dto.Password) ? "changeme" : dto.Password),
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "Maintenance" : dto.Role.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            HomePropertyId = string.IsNullOrWhiteSpace(dto.HomePropertyId) ? null : dto.HomePropertyId.Trim(),
            MaxOpenTickets = dto.MaxOpenTickets <= 0 ? 10 : dto.MaxOpenTickets,
            Active = true
        };

        _db.Employees.Add(e);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/employees/{e.Id}", e);
    }

    public sealed class SetEmployeeSkillsDto
    {
        public string[] Skills { get; set; } = [];
        public int Proficiency { get; set; } = 3;
    }

    [HttpPost("/api/employees/{employeeId:long}/skills")]
    [Authorize(Roles = "Manager,Dispatcher")]
    public async Task<ActionResult<object>> SetEmployeeSkills(long employeeId, [FromBody] SetEmployeeSkillsDto dto, CancellationToken ct)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.Active, ct);
        if (emp is null) return NotFound();

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Plumbing","Electrical","HVAC","Appliance","Structural","Other"
        };
        var skills = (dto.Skills ?? Array.Empty<string>())
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && allowed.Contains(s!))
            .Select(s => allowed.First(a => a.Equals(s!, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var skillRows = await _db.Skills.Where(s => skills.Contains(s.SkillCode)).ToListAsync(ct);
        foreach (var code in skills)
        {
            if (skillRows.Any(s => s.SkillCode == code)) continue;
            var created = new Skill { SkillCode = code, DisplayName = code };
            _db.Skills.Add(created);
            skillRows.Add(created);
        }
        await _db.SaveChangesAsync(ct);

        var existing = await _db.EmployeeSkills.Where(s => s.EmployeeId == employeeId).ToListAsync(ct);
        _db.EmployeeSkills.RemoveRange(existing);
        var prof = dto.Proficiency is >= 1 and <= 5 ? dto.Proficiency : 3;
        foreach (var s in skillRows)
            _db.EmployeeSkills.Add(new EmployeeSkill { EmployeeId = employeeId, SkillId = s.Id, Proficiency = prof });
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, employeeId, skills });
    }

    public sealed class AddNoteDto
    {
        public string? Author { get; set; }
        public string Note { get; set; } = "";
    }

    public sealed class TicketEventDto
    {
        public long Id { get; set; }
        public int RequestNumber { get; set; }
        public string EventType { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? Author { get; set; }
        public string? Note { get; set; }
        public string? DetailsJson { get; set; }
    }

    [HttpPost("{requestNumber:int}/notes")]
    public async Task<ActionResult<object>> AddNote(int requestNumber, [FromBody] AddNoteDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Note))
            return BadRequest(new { message = "note is required" });

        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
        {
            MaintenanceRequestId = t.Id,
            EventType = "note",
            Actor = string.IsNullOrWhiteSpace(dto.Author) ? null : dto.Author.Trim(),
            DetailsJson = JsonSerializer.Serialize(new { note = dto.Note.Trim() }),
            EventTimestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpGet("{requestNumber:int}/events")]
    public async Task<ActionResult<IReadOnlyList<TicketEventDto>>> GetEvents(int requestNumber, CancellationToken ct)
    {
        var list = await _db.MaintenanceRequestEvents.AsNoTracking()
            .Join(
                _db.MaintenanceRequests.AsNoTracking(),
                ev => ev.MaintenanceRequestId,
                r => r.Id,
                (ev, r) => new { ev, r })
            .Where(x => x.r.RequestNumber == requestNumber)
            .OrderByDescending(x => x.ev.EventTimestamp)
            .Take(200)
            .ToListAsync(ct);

        static string? ExtractNote(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(detailsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("note", out var n))
                    return n.GetString();
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var m))
                    return m.GetString();
            }
            catch { }
            return null;
        }

        return Ok(list.Select(x => new TicketEventDto
        {
            Id = x.ev.Id,
            RequestNumber = x.r.RequestNumber,
            EventType = x.ev.EventType,
            CreatedAt = x.ev.EventTimestamp,
            Author = x.ev.Actor,
            Note = ExtractNote(x.ev.DetailsJson),
            DetailsJson = x.ev.DetailsJson
        }).ToList());
    }

    [HttpGet("recent-events")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<TicketEventDto>>> RecentEvents([FromQuery] int? take, CancellationToken ct)
    {
        var n = take is > 0 and <= 200 ? take.Value : 50;

        var list = await _db.MaintenanceRequestEvents.AsNoTracking()
            .Join(
                _db.MaintenanceRequests.AsNoTracking(),
                ev => ev.MaintenanceRequestId,
                r => r.Id,
                (ev, r) => new { ev, r })
            .OrderByDescending(x => x.ev.EventTimestamp)
            .Take(n)
            .ToListAsync(ct);

        static string? ExtractNote(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(detailsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("note", out var n))
                    return n.GetString();
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var m))
                    return m.GetString();
            }
            catch { }
            return null;
        }

        return Ok(list.Select(x => new TicketEventDto
        {
            Id = x.ev.Id,
            RequestNumber = x.r.RequestNumber,
            EventType = x.ev.EventType,
            CreatedAt = x.ev.EventTimestamp,
            Author = x.ev.Actor,
            Note = ExtractNote(x.ev.DetailsJson),
            DetailsJson = x.ev.DetailsJson
        }).ToList());
    }

    public sealed class AssignTicketDto
    {
        public long EmployeeId { get; set; }
        public string? Source { get; set; } // manual|ai|rules
        public string? Author { get; set; }
    }

    [HttpPost("{requestNumber:int}/assign")]
    public async Task<ActionResult<TicketListItemDto>> Assign(int requestNumber, [FromBody] AssignTicketDto dto, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        // Allow unassign with employeeId=0 (keeps the endpoint simple for the dashboard).
        if (dto.EmployeeId <= 0)
        {
            t.AssignedEmployeeId = null;
            t.AssignedAt = null;
            t.AssignmentSource = string.IsNullOrWhiteSpace(dto.Source) ? "manual" : dto.Source.Trim();

            _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
            {
                MaintenanceRequestId = t.Id,
                EventType = "assign",
                Actor = string.IsNullOrWhiteSpace(dto.Author) ? null : dto.Author.Trim(),
                DetailsJson = JsonSerializer.Serialize(new { message = "Unassigned" }),
                EventTimestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            return Ok(MapToDto(t, null));
        }

        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.Active, ct);
        if (emp is null)
            return BadRequest(new { message = "Unknown or inactive employeeId" });

        t.AssignedEmployeeId = emp.Id;
        t.AssignedAt = DateTime.UtcNow;
        t.AssignmentSource = string.IsNullOrWhiteSpace(dto.Source) ? "manual" : dto.Source.Trim();

        _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
        {
            MaintenanceRequestId = t.Id,
            EventType = "assign",
            Actor = string.IsNullOrWhiteSpace(dto.Author) ? null : dto.Author.Trim(),
            DetailsJson = JsonSerializer.Serialize(new { message = $"Assigned to {emp.FullName} (#{emp.Id})" }),
            EventTimestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(t, emp.FullName));
    }

    public sealed class CloseTicketDto
    {
        public string? ClosedBy { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    [HttpPost("{requestNumber:int}/close")]
    public async Task<ActionResult<TicketListItemDto>> Close(int requestNumber, [FromBody] CloseTicketDto dto, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        t.Status = "Closed";
        t.ClosedAt = DateTime.UtcNow;
        t.ClosedBy = string.IsNullOrWhiteSpace(dto.ClosedBy) ? null : dto.ClosedBy.Trim();
        t.ResolutionNotes = string.IsNullOrWhiteSpace(dto.ResolutionNotes) ? null : dto.ResolutionNotes.Trim();

        _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
        {
            MaintenanceRequestId = t.Id,
            EventType = "close",
            Actor = t.ClosedBy,
            DetailsJson = JsonSerializer.Serialize(new { message = string.IsNullOrWhiteSpace(t.ResolutionNotes) ? "Closed" : $"Closed: {t.ResolutionNotes}" }),
            EventTimestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        string? empName = null;
        if (t.AssignedEmployeeId is { } eid)
            empName = await _db.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync(ct);
        return Ok(MapToDto(t, empName));
    }

    [HttpPost("{requestNumber:int}/reopen")]
    public async Task<ActionResult<TicketListItemDto>> Reopen(int requestNumber, CancellationToken ct)
    {
        var t = await _db.MaintenanceRequests.FirstOrDefaultAsync(x => x.RequestNumber == requestNumber, ct);
        if (t is null)
            return NotFound();

        t.Status = "Open";
        t.ClosedAt = null;
        t.ClosedBy = null;
        t.ResolutionNotes = null;

        _db.MaintenanceRequestEvents.Add(new MaintenanceRequestEvent
        {
            MaintenanceRequestId = t.Id,
            EventType = "reopen",
            EventTimestamp = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { message = "Reopened" })
        });

        await _db.SaveChangesAsync(ct);
        string? empName = null;
        if (t.AssignedEmployeeId is { } eid)
            empName = await _db.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync(ct);
        return Ok(MapToDto(t, empName));
    }

    private static TicketListItemDto MapToDto(MaintenanceRequest t, string? assignedEmployeeName)
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
            ImageUrlOrCount = t.ImageUrlOrCount,
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
            UrgencyMatchesSample = urgMatch,
            Status = string.IsNullOrWhiteSpace(t.Status) ? "Open" : t.Status,
            ClosedAt = t.ClosedAt,
            AssignedEmployeeId = t.AssignedEmployeeId,
            AssignedEmployeeName = assignedEmployeeName
        };
    }
}
