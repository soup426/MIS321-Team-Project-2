namespace MulhollandRealEstate.API.Models;

public class MaintenanceRequestImage
{
    public long Id { get; set; }
    public long MaintenanceRequestId { get; set; }
    public string StorageKey { get; set; } = ""; // relative path or opaque key
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

