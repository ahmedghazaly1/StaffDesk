using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string? AccessToken, string? RefreshToken, DateTime? ExpiresAt, string? Role, string? Error)> LoginAsync(
        string username, string password, string? ip, string? userAgent);
    Task<(bool Success, string? Error)> RegisterAsync(string username, string password, string email);
    Task<(bool Success, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task<(bool Success, string? Error)> ChangeRoleAsync(int targetUserId, string newRole, int actorUserId);
}
