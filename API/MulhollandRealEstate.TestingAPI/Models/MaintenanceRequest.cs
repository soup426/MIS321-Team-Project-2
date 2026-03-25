namespace MulhollandRealEstate.API.Models;

public class MaintenanceRequest
{
    public int Id { get; set; }

    /// <summary>Business key from sample data (e.g. 1001).</summary>
    public int RequestNumber { get; set; }

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

    /// <summary>Ground-truth category from the sample dataset (for evaluation).</summary>
    public string ActualCategory { get; set; } = "";

    /// <summary>Ground-truth urgency from the sample dataset.</summary>
    public string ActualUrgency { get; set; } = "";

    public string? PredictedCategory { get; set; }
    public string? PredictedUrgency { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? TagsJson { get; set; }
    public string? RiskNotes { get; set; }
    public bool NeedsHumanReview { get; set; }
    public DateTime? LastTriagedAt { get; set; }
    public string? TriageSource { get; set; }

    // Workflow fields (open/closed + assignment)
    public string Status { get; set; } = "Open"; // Open|Closed
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? ResolutionNotes { get; set; }

    public long? AssignedEmployeeId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public string? AssignmentSource { get; set; }
}
