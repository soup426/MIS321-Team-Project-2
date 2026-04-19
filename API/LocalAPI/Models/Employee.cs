using System.Text.Json.Serialization;

namespace MulhollandRealEstate.API.Models;

public class Employee
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = ""; // login handle (unique)

    [JsonIgnore]
    public string PasswordHash { get; set; } = ""; // PBKDF2 format v1$iter$salt$hash
    public bool Active { get; set; } = true;
    public string Role { get; set; } = "Maintenance";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? HomePropertyId { get; set; }
    public int MaxOpenTickets { get; set; } = 10;
}

