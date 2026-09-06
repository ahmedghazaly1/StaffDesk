using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, Guid sessionId, out DateTime expiresAt)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT signing secret is not configured");
        var issuer = jwtSettings["Issuer"] ?? "StaffDesk";
        var audience = jwtSettings["Audience"] ?? "StaffDeskUsers";
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "15");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("sid", sessionId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public string GetRoleFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        return roleClaim?.Value ?? "Member";
    }

    public bool IsAdmin(string token)
    {
        return GetRoleFromToken(token) == User.Roles.Admin;
    }

    public bool IsAuditor(string token)
    {
        return GetRoleFromToken(token) == User.Roles.Auditor;
    }

    public bool IsHrAdmin(string token)
    {
        return GetRoleFromToken(token) == User.Roles.HrAdmin;
    }
}