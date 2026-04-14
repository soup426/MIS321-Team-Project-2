using System.Text.Json.Serialization;

namespace MulhollandRealEstate.API.Services;

/// <summary>JSON body POSTed to the n8n triage webhook.</summary>
public sealed class N8nTriageRequestDto
{
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
    public string[] ImageUrls { get; set; } = [];
    public int PriorRequestsLast6Mo { get; set; }
    public string ActualCategory { get; set; } = "";
    public string ActualUrgency { get; set; } = "";
}

/// <summary>Expected JSON shape returned by n8n (respond to webhook node or last node output).</summary>
public sealed class N8nTriageResponseDto
{
    [JsonPropertyName("predictedCategory")]
    public string? PredictedCategory { get; set; }

    [JsonPropertyName("predictedUrgency")]
    public string? PredictedUrgency { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("riskNotes")]
    public string? RiskNotes { get; set; }
}
