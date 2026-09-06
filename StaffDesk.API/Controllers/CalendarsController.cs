using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.Filters;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/calendars")]
[Authorize]
public class CalendarsController : ApiControllerBase
{
    private readonly IWorkCalendarRepository _calendars;

    public CalendarsController(IWorkCalendarRepository calendars, IUserRepository userRepository)
        : base(userRepository)
    {
        _calendars = calendars;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _calendars.ListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var cal = await _calendars.GetByIdAsync(id);
        if (cal == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Calendar not found"));
        return Ok(Map(cal));
    }

    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] int departmentId, [FromServices] IWorkingCalendarService calendarService)
    {
        var cal = await calendarService.ResolveCalendarAsync(departmentId);
        return Ok(Map(cal));
    }

    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> Create([FromBody] WorkCalendarCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "name is required"));
        if (dto.WorkEndHour <= dto.WorkStartHour)
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "workEndHour must be greater than workStartHour"));

        var cal = await _calendars.CreateAsync(new WorkCalendar
        {
            Name = dto.Name.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "UTC" : dto.TimeZoneId.Trim(),
            WorkDaysMask = dto.WorkDaysMask ?? WorkDayFlags.Weekdays,
            WorkStartHour = dto.WorkStartHour ?? 9,
            WorkEndHour = dto.WorkEndHour ?? 17,
            IsOrganizationDefault = dto.IsOrganizationDefault ?? false,
            EffectiveFrom = dto.EffectiveFrom ?? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = dto.EffectiveTo
        });

        return CreatedAtAction(nameof(Get), new { id = cal.Id }, Map(cal));
    }

    [HttpPut("{id:int}")]
    [AdminOnly]
    public async Task<IActionResult> Update(int id, [FromBody] WorkCalendarCreateDto dto)
    {
        var cal = await _calendars.GetByIdAsync(id);
        if (cal == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Calendar not found"));
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "name is required"));

        cal.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TimeZoneId)) cal.TimeZoneId = dto.TimeZoneId.Trim();
        if (dto.WorkDaysMask.HasValue) cal.WorkDaysMask = dto.WorkDaysMask.Value;
        if (dto.WorkStartHour.HasValue) cal.WorkStartHour = dto.WorkStartHour.Value;
        if (dto.WorkEndHour.HasValue) cal.WorkEndHour = dto.WorkEndHour.Value;
        if (dto.IsOrganizationDefault.HasValue) cal.IsOrganizationDefault = dto.IsOrganizationDefault.Value;
        if (dto.EffectiveFrom.HasValue) cal.EffectiveFrom = dto.EffectiveFrom.Value;
        cal.EffectiveTo = dto.EffectiveTo;

        await _calendars.UpdateAsync(cal);
        return Ok(Map(cal));
    }

    [HttpPost("{id:int}/holidays")]
    [AdminOnly]
    public async Task<IActionResult> AddHoliday(int id, [FromBody] HolidayCreateDto dto)
    {
        var cal = await _calendars.GetByIdAsync(id);
        if (cal == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Calendar not found"));
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "name is required"));

        var holiday = await _calendars.AddHolidayAsync(new CalendarHoliday
        {
            CalendarId = id,
            Date = dto.Date,
            Name = dto.Name.Trim(),
            RecursAnnually = dto.RecursAnnually
        });

        return Ok(new { holiday.Id, holiday.Date, holiday.Name, holiday.RecursAnnually });
    }

    [HttpDelete("holidays/{holidayId:int}")]
    [AdminOnly]
    public async Task<IActionResult> RemoveHoliday(int holidayId)
    {
        var ok = await _calendars.RemoveHolidayAsync(holidayId);
        if (!ok)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Holiday not found"));
        return NoContent();
    }

    [HttpPut("departments/{departmentId:int}")]
    [AdminOnly]
    public async Task<IActionResult> SetDepartmentCalendar(int departmentId, [FromBody] SetDepartmentCalendarDto dto)
    {
        try
        {
            await _calendars.SetDepartmentCalendarAsync(departmentId, dto.CalendarId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
        }
    }

    private static object Map(WorkCalendar c) => new
    {
        c.Id,
        c.Name,
        c.TimeZoneId,
        c.WorkDaysMask,
        c.WorkStartHour,
        c.WorkEndHour,
        c.IsOrganizationDefault,
        c.EffectiveFrom,
        c.EffectiveTo,
        holidays = (c.Holidays ?? Array.Empty<CalendarHoliday>()).Select(h => new
        {
            h.Id, h.Date, h.Name, h.RecursAnnually
        })
    };
}

public class WorkCalendarCreateDto
{
    public string Name { get; set; } = "";
    public string? TimeZoneId { get; set; }
    public int? WorkDaysMask { get; set; }
    public int? WorkStartHour { get; set; }
    public int? WorkEndHour { get; set; }
    public bool? IsOrganizationDefault { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class HolidayCreateDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
    public bool RecursAnnually { get; set; }
}

public class SetDepartmentCalendarDto
{
    public int? CalendarId { get; set; }
}
