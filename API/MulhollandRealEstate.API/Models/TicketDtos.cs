namespace MulhollandRealEstate.API.Models;

public class TicketListItemDto
{
    public int Id { get; set; }
    public int RequestNumber { get; set; }
    public string PropertyId { get; set; } = "";
    public string UnitNumber { get; set; } = "";
    public string BuildingType { get; set; } = "";
    public DateTime RequestTimestamp { get; set; }
    public string RequestText { get; set; } = "";
    public bool HasImage { get; set; }
    public string ActualCategory { get; set; } = "";
    public string ActualUrgency { get; set; } = "";
    public string? PredictedCategory { get; set; }
    public string? PredictedUrgency { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string[] Tags { get; set; } = [];
    public string? RiskNotes { get; set; }
    public bool NeedsHumanReview { get; set; }
    public DateTime? LastTriagedAt { get; set; }
    public string? TriageSource { get; set; }
    public bool? CategoryMatchesSample { get; set; }
    public bool? UrgencyMatchesSample { get; set; }
}

public class TriageSummaryDto
{
    public int Total { get; set; }
    public int Triaged { get; set; }
    public int NeedsHumanReview { get; set; }
    public Dictionary<string, int> ByPredictedUrgency { get; set; } = new();
    public int CategoryMatches { get; set; }
    public int UrgencyMatches { get; set; }
    public int ComparedRows { get; set; }
}
