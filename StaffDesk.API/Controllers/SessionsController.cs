using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/auth/sessions")]
[Authorize]
public class SessionsController : ApiControllerBase
{
    private readonly IAuthSessionService _sessions;

    public SessionsController(IAuthSessionService sessions, IUserRepository userRepository)
        : base(userRepository)
    {
        _sessions = sessions;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListMine([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var currentSid = GetSessionId();
        var rows = await _sessions.ListMineAsync(userId.Value);
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);
        var total = rows.Count;
        var slice = rows.Skip((page - 1) * limit).Take(limit).ToList();
        return Ok(new
        {
            data = slice.Select(s => new
            {
                s.Id,
                s.CreatedAt,
                s.LastSeenAt,
                s.Ip,
                s.UserAgent,
                current = currentSid == s.Id
            }),
            page,
            limit,
            totalCount = total
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeMine(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var ok = await _sessions.RevokeOwnAsync(userId.Value, id);
        if (!ok)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Not found"));
        return Ok(new { revoked = id });
    }

    [HttpDelete("employee/{employeeId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeEmployee(int employeeId)
    {
        var n = await _sessions.RevokeAllForEmployeeAsync(employeeId, "admin_bulk_revoke");
        return Ok(new { employeeId, revoked = n > 0 });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private Guid? GetSessionId()
    {
        var sid = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sid, out var id) ? id : null;
    }
}
