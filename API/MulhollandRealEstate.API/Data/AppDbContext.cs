using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Models;

namespace MulhollandRealEstate.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<MaintenanceRequestEvent> MaintenanceRequestEvents => Set<MaintenanceRequestEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<MaintenanceRequest>();

        // Map to the same snake_case table/columns that n8n writes to.
        e.ToTable("maintenance_requests");

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.RequestNumber).HasColumnName("request_number");
        e.Property(x => x.PropertyId).HasColumnName("property_id");
        e.Property(x => x.UnitNumber).HasColumnName("unit_number");
        e.Property(x => x.BuildingType).HasColumnName("building_type");
        e.Property(x => x.TenantTenureMonths).HasColumnName("tenant_tenure_months");
        e.Property(x => x.SubmissionChannel).HasColumnName("submission_channel");
        e.Property(x => x.RequestTimestamp).HasColumnName("request_timestamp");
        e.Property(x => x.RequestText).HasColumnName("request_text");
        e.Property(x => x.HasImage).HasColumnName("has_image");
        e.Property(x => x.ImageType).HasColumnName("image_type");
        e.Property(x => x.ImageSeverityHint).HasColumnName("image_severity_hint");
        e.Property(x => x.ImageUrlOrCount).HasColumnName("image_url_or_count");
        e.Property(x => x.PriorRequestsLast6Mo).HasColumnName("prior_requests_last_6mo");
        e.Property(x => x.ActualCategory).HasColumnName("actual_category");
        e.Property(x => x.ActualUrgency).HasColumnName("actual_urgency");
        e.Property(x => x.PredictedCategory).HasColumnName("predicted_category");
        e.Property(x => x.PredictedUrgency).HasColumnName("predicted_urgency");
        e.Property(x => x.ConfidenceScore).HasColumnName("confidence_score");
        e.Property(x => x.TagsJson).HasColumnName("tags_json");
        e.Property(x => x.RiskNotes).HasColumnName("risk_notes");
        e.Property(x => x.NeedsHumanReview).HasColumnName("needs_human_review");
        e.Property(x => x.LastTriagedAt).HasColumnName("last_triaged_at");
        e.Property(x => x.TriageSource).HasColumnName("triage_source");
        e.Property(x => x.Status).HasColumnName("status");
        e.Property(x => x.ClosedAt).HasColumnName("closed_at");
        e.Property(x => x.ClosedBy).HasColumnName("closed_by");
        e.Property(x => x.ResolutionNotes).HasColumnName("resolution_notes");
        e.Property(x => x.AssignedEmployeeId).HasColumnName("assigned_employee_id");
        e.Property(x => x.AssignedAt).HasColumnName("assigned_at");
        e.Property(x => x.AssignmentSource).HasColumnName("assignment_source");

        e.HasIndex(x => x.RequestNumber)
            .IsUnique()
            .HasDatabaseName("uq_maintenance_requests_request_number");

        // Keep length/precision aligned with SQL schema.
        e.Property(x => x.RequestText).HasMaxLength(4000);
        e.Property(x => x.PropertyId).HasMaxLength(32);
        e.Property(x => x.UnitNumber).HasMaxLength(32);
        e.Property(x => x.BuildingType).HasMaxLength(64);
        e.Property(x => x.SubmissionChannel).HasMaxLength(32);
        e.Property(x => x.ImageType).HasMaxLength(64);
        e.Property(x => x.ImageSeverityHint).HasMaxLength(32);
        e.Property(x => x.ActualCategory).HasMaxLength(64);
        e.Property(x => x.ActualUrgency).HasMaxLength(32);
        e.Property(x => x.PredictedCategory).HasMaxLength(64);
        e.Property(x => x.PredictedUrgency).HasMaxLength(32);
        e.Property(x => x.TriageSource).HasMaxLength(32);
        e.Property(x => x.RiskNotes).HasMaxLength(2000);
        e.Property(x => x.TagsJson).HasMaxLength(1000);
        e.Property(x => x.ConfidenceScore).HasPrecision(5, 4);
        e.Property(x => x.Status).HasMaxLength(16);
        e.Property(x => x.ClosedBy).HasMaxLength(100);
        e.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        e.Property(x => x.AssignmentSource).HasMaxLength(32);

        var emp = modelBuilder.Entity<Employee>();
        emp.ToTable("employees");
        emp.Property(x => x.Id).HasColumnName("id");
        emp.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(120);
        emp.Property(x => x.Active).HasColumnName("active");
        emp.Property(x => x.Role).HasColumnName("role").HasMaxLength(60);
        emp.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40);
        emp.Property(x => x.Email).HasColumnName("email").HasMaxLength(120);
        emp.Property(x => x.HomePropertyId).HasColumnName("home_property_id").HasMaxLength(32);
        emp.Property(x => x.MaxOpenTickets).HasColumnName("max_open_tickets");

        var ev = modelBuilder.Entity<MaintenanceRequestEvent>();
        ev.ToTable("maintenance_request_events");
        ev.Property(x => x.Id).HasColumnName("id");
        ev.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(40);
        ev.Property(x => x.MaintenanceRequestId).HasColumnName("maintenance_request_id");
        ev.Property(x => x.EventTimestamp).HasColumnName("event_timestamp");
        ev.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(128);
        ev.Property(x => x.DetailsJson).HasColumnName("details_json");
    }
}
