namespace MulhollandRealEstate.API.Models;

public class Skill
{
    public long Id { get; set; }
    public string SkillCode { get; set; } = "";   // Plumbing|Electrical|HVAC|Appliance|Structural|Other
    public string DisplayName { get; set; } = "";
}

