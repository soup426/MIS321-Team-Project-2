using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Models;
using MulhollandRealEstate.API.Services;

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

    public static async Task SeedDemoUsersAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Skills
        var skills = new[]
        {
            new Skill { SkillCode = "plumbing", DisplayName = "Plumbing" },
            new Skill { SkillCode = "electrical", DisplayName = "Electrical" },
            new Skill { SkillCode = "hvac", DisplayName = "HVAC" },
            new Skill { SkillCode = "appliance", DisplayName = "Appliance" },
            new Skill { SkillCode = "pest", DisplayName = "Pest control" },
            new Skill { SkillCode = "drywall", DisplayName = "Drywall / paint" },
        };

        foreach (var s in skills)
        {
            if (!await db.Skills.AnyAsync(x => x.SkillCode == s.SkillCode, ct))
                db.Skills.Add(s);
        }
        await db.SaveChangesAsync(ct);

        var byCode = await db.Skills.AsNoTracking()
            .Where(s => s.SkillCode != null && s.SkillCode != "")
            .ToDictionaryAsync(s => s.SkillCode, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);

        // If the DB was left half-seeded (crash), ensure required skill codes exist.
        foreach (var s in skills)
        {
            if (byCode.ContainsKey(s.SkillCode)) continue;
            db.Skills.Add(new Skill { SkillCode = s.SkillCode, DisplayName = s.DisplayName });
        }
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
            byCode = await db.Skills.AsNoTracking()
                .Where(s => s.SkillCode != null && s.SkillCode != "")
                .ToDictionaryAsync(s => s.SkillCode, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);
        }

        const string demoPassword = "demo1234!";

        var employees = new[]
        {
            new Employee { FullName = "Alex Tech", Username = "alex", Role = "Maintenance", Active = true, MaxOpenTickets = 8, PasswordHash = PasswordHashing.Hash(demoPassword) },
            new Employee { FullName = "Taylor Plumber", Username = "taylor", Role = "Maintenance", Active = true, MaxOpenTickets = 8, PasswordHash = PasswordHashing.Hash(demoPassword) },
            new Employee { FullName = "Sam Electric", Username = "sam", Role = "Maintenance", Active = true, MaxOpenTickets = 8, PasswordHash = PasswordHashing.Hash(demoPassword) },
            new Employee { FullName = "Pat HVAC", Username = "pat", Role = "Maintenance", Active = true, MaxOpenTickets = 8, PasswordHash = PasswordHashing.Hash(demoPassword) },
            new Employee { FullName = "Morgan Manager", Username = "morgan", Role = "Manager", Active = true, MaxOpenTickets = 20, PasswordHash = PasswordHashing.Hash(demoPassword) },
        };

        foreach (var e in employees)
        {
            var existing = await db.Employees.FirstOrDefaultAsync(x => x.Username == e.Username, ct);
            if (existing is null)
            {
                db.Employees.Add(e);
            }
            else
            {
                // Fill missing auth fields for old demo rows.
                existing.Active = true;
                if (string.IsNullOrWhiteSpace(existing.FullName)) existing.FullName = e.FullName;
                if (string.IsNullOrWhiteSpace(existing.Role)) existing.Role = e.Role;
                if (existing.MaxOpenTickets <= 0) existing.MaxOpenTickets = e.MaxOpenTickets;
                if (string.IsNullOrWhiteSpace(existing.PasswordHash)) existing.PasswordHash = PasswordHashing.Hash(demoPassword);
            }
        }
        await db.SaveChangesAsync(ct);

        var empByUsername = await db.Employees.AsNoTracking()
            .Where(e => e.Username != null && e.Username != "")
            .ToDictionaryAsync(e => e.Username, e => e.Id, ct);

        // Skill mappings (proficiency 1-5)
        var mappings = new[]
        {
            new EmployeeSkill { EmployeeId = empByUsername["alex"], SkillId = byCode["appliance"], Proficiency = 4 },
            new EmployeeSkill { EmployeeId = empByUsername["alex"], SkillId = byCode["drywall"], Proficiency = 3 },
            new EmployeeSkill { EmployeeId = empByUsername["taylor"], SkillId = byCode["plumbing"], Proficiency = 5 },
            new EmployeeSkill { EmployeeId = empByUsername["sam"], SkillId = byCode["electrical"], Proficiency = 5 },
            new EmployeeSkill { EmployeeId = empByUsername["pat"], SkillId = byCode["hvac"], Proficiency = 5 },
            new EmployeeSkill { EmployeeId = empByUsername["pat"], SkillId = byCode["appliance"], Proficiency = 3 },
            new EmployeeSkill { EmployeeId = empByUsername["alex"], SkillId = byCode["pest"], Proficiency = 2 },
        };

        foreach (var m in mappings)
        {
            if (!await db.EmployeeSkills.AnyAsync(x => x.EmployeeId == m.EmployeeId && x.SkillId == m.SkillId, ct))
                db.EmployeeSkills.Add(m);
        }

        await db.SaveChangesAsync(ct);
    }
}
