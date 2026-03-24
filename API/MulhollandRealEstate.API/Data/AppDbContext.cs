using Microsoft.EntityFrameworkCore;
using MulhollandRealEstate.API.Models;

namespace MulhollandRealEstate.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<MaintenanceRequest>();
        e.HasIndex(x => x.RequestNumber).IsUnique();
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
    }
}
