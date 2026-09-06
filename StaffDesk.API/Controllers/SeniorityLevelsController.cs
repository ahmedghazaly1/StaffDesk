using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.API.Filters;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/seniority-levels")]
[Authorize]
public class SeniorityLevelsController : ControllerBase
{
    private readonly ISeniorityLevelService _service;

    public SeniorityLevelsController(ISeniorityLevelService service)
    {
        _service = service;
    }

    // GET /v1/seniority-levels - List all levels (Anyone can view)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var levels = await _service.GetAllAsync();
        return Ok(levels);
    }

    // GET /v1/seniority-levels/{id} - Get a single level
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var level = await _service.GetByIdAsync(id);
        if (level == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Seniority level not found"));

        return Ok(level);
    }

    // POST /v1/seniority-levels - Create a new level (Admin only)
    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> Create([FromBody] CreateSeniorityLevelRequest request)
    {
        try
        {
            var level = await _service.CreateAsync(
                request.Name,
                request.Rank,
                request.Description
            );
            return CreatedAtAction(nameof(GetById), new { id = level.Id }, level);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // PUT /v1/seniority-levels/{id} - Update a level (Admin only)
    [HttpPut("{id}")]
    [AdminOnly]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSeniorityLevelRequest request)
    {
        try
        {
            var level = await _service.UpdateAsync(
                id,
                request.Name,
                request.Rank,
                request.Description,
                request.IsActive
            );
            return Ok(level);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // DELETE /v1/seniority-levels/{id} - Delete a level (Admin only)
    [HttpDelete("{id}")]
    [AdminOnly]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Seniority level not found"));

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiError.Build(TaskErrorCodes.DuplicateResource, ex.Message));
        }
    }
}

// ============================================
// Request DTOs
// ============================================

public class CreateSeniorityLevelRequest
{
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string? Description { get; set; }
}

public class UpdateSeniorityLevelRequest
{
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}