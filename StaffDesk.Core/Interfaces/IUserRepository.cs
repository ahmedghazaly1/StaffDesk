using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User> CreateAsync(User user);
    Task<bool> UserExistsAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmployeeIdAsync(int employeeId);
    Task UpdateAsync(User user);
}