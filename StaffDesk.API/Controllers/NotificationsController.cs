using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly ITaskService _taskService;

    public NotificationsController(ITaskService taskService, IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
    }

    // GET /v1/notifications?unread=true
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool? unread = null, [FromQuery] int? page = 1, [FromQuery] int? limit = 25, [FromQuery] string? cursor = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var unreadCount = await _taskService.GetUnreadNotificationCountAsync(employeeId.Value);

        // PL-8: cursor pagination alongside the existing page-based scheme.
        if (cursor != null || Request.Query.ContainsKey("cursor"))
        {
            var (items, nextCursor) = await _taskService.GetMyNotificationsCursorAsync(employeeId.Value, unread, cursor, limit ?? 25);
            return Ok(new { data = items.Select(MapNotification), unreadCount, nextCursor });
        }

        var notifications = await _taskService.GetMyNotificationsAsync(employeeId.Value, unread, page, limit);
        var data = notifications.Select(MapNotification).ToList();

        return Ok(new NotificationListResponseDto
        {
            Data = data,
            UnreadCount = unreadCount,
            Page = page ?? 1,
            Limit = limit ?? 25,
            Total = data.Count
        });
    }

    private static NotificationResponseDto MapNotification(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Message = n.Message,
        TaskId = n.TaskId,
        TaskKey = n.Task?.Key,
        TaskTitle = n.Task?.Title,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };

    // PATCH /v1/notifications/{id}/read
    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var marked = await _taskService.MarkNotificationReadAsync(id, employeeId.Value);
        if (!marked)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Notification not found"));

        return NoContent();
    }

    // POST /v1/notifications/read-all
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        await _taskService.MarkAllNotificationsReadAsync(employeeId.Value);
        return NoContent();
    }
}
