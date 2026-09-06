using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Common;

// Shared base for controllers that need to resolve the caller's Employee.Id from the JWT.
// Reuses the exact pattern established in the original TasksController: User (JWT NameIdentifier)
// -> User.EmployeeId, NOT the old broken "assume User.Id == Employee.Id" shortcut.
[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly IUserRepository UserRepository;

    protected ApiControllerBase(IUserRepository userRepository)
    {
        UserRepository = userRepository;
    }

    protected async Task<int?> GetCurrentEmployeeIdAsync()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId))
            return null;

        var user = await UserRepository.GetByIdAsync(userId);
        return user?.EmployeeId;
    }

    protected IActionResult NoEmployeeLinkError()
    {
        return StatusCode(403, ApiError.Build(TaskErrorCodes.NoEmployeeLink,
            "Your account is not linked to an employee record, so you cannot act on tasks."));
    }
}
