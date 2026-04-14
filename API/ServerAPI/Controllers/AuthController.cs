using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MulhollandRealEstate.API.Data;
using MulhollandRealEstate.API.Models;
using MulhollandRealEstate.API.Services;

namespace MulhollandRealEstate.API.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public sealed class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    [HttpPost("/api/auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "username and password are required" });

        var username = dto.Username.Trim();
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Active && e.Username == username, ct);
        if (emp is null || !PasswordHashing.Verify(dto.Password, emp.PasswordHash))
            return Unauthorized(new { message = "invalid credentials" });

        var token = IssueJwt(emp);
        return Ok(new
        {
            accessToken = token,
            employee = new { id = emp.Id, fullName = emp.FullName, username = emp.Username, role = emp.Role }
        });
    }

    public sealed class RegisterDto
    {
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "Maintenance";
        public int MaxOpenTickets { get; set; } = 10;
    }

    [HttpPost("/api/auth/register")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var allow = _config.GetValue("Auth:AllowOpenRegistration", false);
        if (!allow) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "fullName, username, password required" });

        var username = dto.Username.Trim();
        var exists = await _db.Employees.AnyAsync(e => e.Username == username, ct);
        if (exists) return Conflict(new { message = "username already exists" });

        var emp = new Employee
        {
            FullName = dto.FullName.Trim(),
            Username = username,
            PasswordHash = PasswordHashing.Hash(dto.Password),
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "Maintenance" : dto.Role.Trim(),
            MaxOpenTickets = dto.MaxOpenTickets <= 0 ? 10 : dto.MaxOpenTickets,
            Active = true
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync(ct);

        return Created("/api/me", new { id = emp.Id, fullName = emp.FullName, username = emp.Username, role = emp.Role });
    }

    [HttpGet("/api/me")]
    [Authorize]
    public async Task<ActionResult<object>> Me(CancellationToken ct)
    {
        var idStr = User.FindFirstValue("employeeId");
        if (!long.TryParse(idStr, out var id)) return Unauthorized();

        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.Active, ct);
        if (emp is null) return Unauthorized();
        return Ok(new { id = emp.Id, fullName = emp.FullName, username = emp.Username, role = emp.Role });
    }

    private string IssueJwt(Employee emp)
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jwt:Key missing");

        var issuer = _config["Jwt:Issuer"] ?? "MulhollandRealEstate";
        var audience = _config["Jwt:Audience"] ?? "MulhollandRealEstate";
        var expMin = _config.GetValue("Jwt:ExpMinutes", 240);

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("employeeId", emp.Id.ToString()),
            new Claim(ClaimTypes.Name, emp.Username),
            new Claim(ClaimTypes.Role, emp.Role ?? "Maintenance")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMin),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

