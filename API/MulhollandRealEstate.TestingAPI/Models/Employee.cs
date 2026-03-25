namespace MulhollandRealEstate.API.Models;

public class Employee
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public bool Active { get; set; } = true;
    public string Role { get; set; } = "Maintenance";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? HomePropertyId { get; set; }
    public int MaxOpenTickets { get; set; } = 10;
}

