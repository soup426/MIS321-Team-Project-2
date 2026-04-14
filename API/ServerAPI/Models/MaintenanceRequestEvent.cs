namespace MulhollandRealEstate.API.Models;

public class MaintenanceRequestEvent
{
    public long Id { get; set; }
    public long MaintenanceRequestId { get; set; }
    public string EventType { get; set; } = "note"; // note|assign|close|reopen|triage
    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;
    public string? Actor { get; set; }
    public string? DetailsJson { get; set; } // JSON string
}

