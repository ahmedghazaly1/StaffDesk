using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/comments")]
[Authorize]
public class CommentsController : ApiControllerBase
{
    private readonly ITaskService _taskService;

    public CommentsController(ITaskService taskService, IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
    }

    // PATCH /v1/comments/{id} - author-only edit
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateComment(int id, [FromBody] CommentUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var comment = await _taskService.UpdateCommentAsync(id, employeeId.Value, dto.Body);
            return Ok(new CommentResponseDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                Body = comment.Body,
                AuthorId = comment.AuthorId,
                AuthorName = comment.Author?.FullName ?? string.Empty,
                AuthorLevel = comment.Author?.Level?.Name ?? string.Empty,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsDeleted = comment.DeletedAt.HasValue
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // DELETE /v1/comments/{id} - soft delete, author-only
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteCommentAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Comment not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }
}
