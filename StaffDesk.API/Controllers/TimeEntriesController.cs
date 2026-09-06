using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/time-entries")]
[Authorize]
public class TimeEntriesController : ApiControllerBase
{
    private readonly ITaskService _taskService;

    public TimeEntriesController(ITaskService taskService, IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
    }

    // DELETE /v1/time-entries/{id} - own entries only (enforced in TaskService/repository)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTimeEntry(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteTimeEntryAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Time entry not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }
}
