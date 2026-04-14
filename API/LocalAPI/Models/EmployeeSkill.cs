namespace MulhollandRealEstate.API.Models;

public class EmployeeSkill
{
    public long EmployeeId { get; set; }
    public long SkillId { get; set; }
    public int Proficiency { get; set; } = 3; // 1-5
}

