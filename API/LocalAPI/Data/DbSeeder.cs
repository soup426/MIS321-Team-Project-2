using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Models;

namespace MulhollandRealEstate.API.Data;

public static class DbSeeder
{
    private sealed class SeedRow
    {
        public int RequestId { get; set; }
        public string PropertyId { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string BuildingType { get; set; } = "";
        public int TenantTenureMonths { get; set; }
        public string SubmissionChannel { get; set; } = "";
        public DateTime RequestTimestamp { get; set; }
        public string RequestText { get; set; } = "";
        public bool HasImage { get; set; }
        public string ImageType { get; set; } = "";
        public string ImageSeverityHint { get; set; } = "";
        public string? ImageUrlOrCount { get; set; }
        public int PriorRequestsLast6Mo { get; set; }
        public string ActualCategory { get; set; } = "";
        public string ActualUrgency { get; set; } = "";
    }

    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment env, CancellationToken ct = default)
    {
        if (await db.MaintenanceRequests.AnyAsync(ct))
            return;

        var path = Path.Combine(env.ContentRootPath, "Data", "maintenance-seed.json");
        if (!File.Exists(path))
            return;

        await using var stream = File.OpenRead(path);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rows = await JsonSerializer.DeserializeAsync<List<SeedRow>>(stream, jsonOptions, cancellationToken: ct);
        if (rows is null || rows.Count == 0)
            return;

        foreach (var r in rows)
        {
            db.MaintenanceRequests.Add(new MaintenanceRequest
            {
                RequestNumber = r.RequestId,
                PropertyId = r.PropertyId,
                UnitNumber = r.UnitNumber,
                BuildingType = r.BuildingType,
                TenantTenureMonths = r.TenantTenureMonths,
                SubmissionChannel = r.SubmissionChannel,
                RequestTimestamp = r.RequestTimestamp,
                RequestText = r.RequestText,
                HasImage = r.HasImage,
                ImageType = r.ImageType,
                ImageSeverityHint = r.ImageSeverityHint,
                ImageUrlOrCount = r.ImageUrlOrCount,
                PriorRequestsLast6Mo = r.PriorRequestsLast6Mo,
                ActualCategory = r.ActualCategory,
                ActualUrgency = r.ActualUrgency
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
