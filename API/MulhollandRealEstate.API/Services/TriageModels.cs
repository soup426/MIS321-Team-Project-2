namespace MulhollandRealEstate.API.Services;

public sealed class TriageResult
{
    public string PredictedCategory { get; set; } = "";
    public string PredictedUrgency { get; set; } = "";
    public double Confidence { get; set; }
    public string[] Tags { get; set; } = [];
    public string RiskNotes { get; set; } = "";
    public string Source { get; set; } = "";
}
