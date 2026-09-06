using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user, Guid sessionId, out DateTime expiresAt);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    string GetRoleFromToken(string token);
    bool IsAdmin(string token);
    bool IsAuditor(string token);   
    bool IsHrAdmin(string token);
}