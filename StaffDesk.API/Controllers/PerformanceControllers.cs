using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/review-cycles")]
[Authorize]
public class ReviewCyclesController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public ReviewCyclesController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewCycleRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            var created = await _perf.CreateCycleAsync(actor.EmployeeId!.Value, actor.Role!, body);
            var id = (int)created.GetType().GetProperty("Id")!.GetValue(created)!;
            return Created($"/v1/review-cycles/{id}", created);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id:int}/stage")]
    public async Task<IActionResult> Advance(int id, [FromBody] AdvanceStageRequest? body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            var result = await _perf.AdvanceStageAsync(actor.EmployeeId!.Value, actor.Role!, id, body ?? new AdvanceStageRequest());
            return Ok(result);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id:int}/stage/reverse")]
    public async Task<IActionResult> ReverseStage(int id, [FromBody] AdvanceStageRequest? body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            var result = await _perf.ReverseStageAsync(actor.EmployeeId!.Value, actor.Role!, id, body ?? new AdvanceStageRequest());
            return Ok(result);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("{id:int}/progress")]
    public async Task<IActionResult> Progress(int id)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            return Ok(await _perf.GetProgressAsync(actor.EmployeeId!.Value, actor.Role!, id));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    private async Task<(int? EmployeeId, string? Role, IActionResult? Result)> RequireActorAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return (null, null, NoEmployeeLinkError());
        var role = await GetActorRoleAsync();
        return (employeeId, role, null);
    }

    private async Task<string> GetActorRoleAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return "";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "";
    }
}

[ApiController]
[Route("v1/reviews")]
[Authorize]
public class ReviewsController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public ReviewsController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return Ok(await _perf.GetMyReviewsAsync(actor.EmployeeId!.Value, actor.Role!)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            var result = await _perf.GetReviewAsync(actor.EmployeeId!.Value, actor.Role!, id);
            await SetReviewETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPut("{id:int}/self-assessment")]
    public async Task<IActionResult> SelfAssessment(int id, [FromBody] AssessmentRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        var currentUpdatedAt = await _perf.GetReviewUpdatedAtAsync(id);
        if (currentUpdatedAt == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Review not found"));
        if (StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(Request, currentUpdatedAt.Value) is { } pre)
            return StatusCode(pre.Status, pre.Body);
        try
        {
            var result = await _perf.SaveSelfAssessmentAsync(actor.EmployeeId!.Value, actor.Role!, id, body);
            await SetReviewETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPut("{id:int}/manager-assessment")]
    public async Task<IActionResult> ManagerAssessment(int id, [FromBody] AssessmentRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        var currentUpdatedAt = await _perf.GetReviewUpdatedAtAsync(id);
        if (currentUpdatedAt == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Review not found"));
        if (StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(Request, currentUpdatedAt.Value) is { } pre)
            return StatusCode(pre.Status, pre.Body);
        try
        {
            var result = await _perf.SaveManagerAssessmentAsync(actor.EmployeeId!.Value, actor.Role!, id, body);
            await SetReviewETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/reassign-reviewer")]
    public async Task<IActionResult> ReassignReviewer(int id, [FromBody] ReassignReviewerRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return Ok(await _perf.ReassignReviewerAsync(actor.EmployeeId!.Value, actor.Role!, id, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/evidence")]
    public async Task<IActionResult> Evidence(int id, [FromBody] AttachEvidenceRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try
        {
            var created = await _perf.AttachEvidenceAsync(actor.EmployeeId!.Value, actor.Role!, id, body);
            return StatusCode(201, created);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/response")]
    public async Task<IActionResult> AddEmployeeResponse(int id, [FromBody] ReviewResponseRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return StatusCode(201, await _perf.AddResponseAsync(actor.EmployeeId!.Value, actor.Role!, id, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/appeal")]
    public async Task<IActionResult> Appeal(int id, [FromBody] AppealRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return StatusCode(201, await _perf.RaiseAppealAsync(actor.EmployeeId!.Value, actor.Role!, id, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/appeal/{appealId:int}/decide")]
    public async Task<IActionResult> DecideAppeal(int id, int appealId, [FromBody] DecideAppealRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return Ok(await _perf.DecideAppealAsync(actor.EmployeeId!.Value, actor.Role!, id, appealId, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/peer-invitations")]
    public async Task<IActionResult> PeerInvitations(int id, [FromBody] PeerNominationRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        try { return StatusCode(201, await _perf.NominatePeersAsync(actor.EmployeeId!.Value, actor.Role!, id, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPatch("{id:int}/calibrated-rating")]
    public async Task<IActionResult> Calibrate(int id, [FromBody] CalibrateRequest body)
    {
        var actor = await RequireActorAsync();
        if (actor.Result != null) return actor.Result;
        var currentUpdatedAt = await _perf.GetReviewUpdatedAtAsync(id);
        if (currentUpdatedAt == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Review not found"));
        if (StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(Request, currentUpdatedAt.Value) is { } pre)
            return StatusCode(pre.Status, pre.Body);
        try
        {
            var result = await _perf.CalibrateRatingAsync(actor.EmployeeId!.Value, actor.Role!, id, body);
            await SetReviewETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    private async Task SetReviewETagAsync(int reviewId)
    {
        var updatedAt = await _perf.GetReviewUpdatedAtAsync(reviewId);
        if (updatedAt != null)
            StaffDesk.API.Common.ConcurrencyHelper.SetETag(Response, updatedAt.Value);
    }

    private async Task<(int? EmployeeId, string? Role, IActionResult? Result)> RequireActorAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return (null, null, NoEmployeeLinkError());
        var role = await GetActorRoleAsync();
        return (employeeId, role, null);
    }

    private async Task<string> GetActorRoleAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return "";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "";
    }
}

[ApiController]
[Route("v1/peer-feedback")]
[Authorize]
public class PeerFeedbackController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public PeerFeedbackController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpPost("{token}")]
    public async Task<IActionResult> Submit(string token, [FromBody] PeerFeedbackRequest body)
    {
        try { return StatusCode(201, await _perf.SubmitPeerFeedbackAsync(token, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }
}

[ApiController]
[Route("v1/calibration")]
[Authorize]
public class CalibrationController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public CalibrationController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpGet("{cycleId:int}")]
    public async Task<IActionResult> Get(int cycleId, [FromQuery] int? departmentId = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        try { return Ok(await _perf.GetCalibrationAsync(employeeId.Value, role, cycleId, departmentId)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    private async Task<string> GetActorRoleAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return "";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "";
    }
}

[ApiController]
[Route("v1/goals")]
[Authorize]
public class GoalsController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public GoalsController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    // GET /v1/goals/{id} — PL-4: returns the goal with an ETag so callers can supply If-Match on writes.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        try
        {
            var goal = await _perf.GetGoalAsync(employeeId.Value, role, id);
            if (goal == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Goal not found"));
            await SetGoalETagAsync(id);
            return Ok(goal);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest body)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        try
        {
            var created = await _perf.CreateGoalAsync(employeeId.Value, role, body);
            var id = (int)created.GetType().GetProperty("Id")!.GetValue(created)!;
            await SetGoalETagAsync(id);
            return Created($"/v1/goals/{id}", created);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        try
        {
            var result = await _perf.AcknowledgeGoalAsync(employeeId.Value, role, id);
            await SetGoalETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    [HttpPatch("{id:int}/progress")]
    public async Task<IActionResult> Progress(int id, [FromBody] GoalProgressRequest body)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        var currentUpdatedAt = await _perf.GetGoalUpdatedAtAsync(id);
        if (currentUpdatedAt == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Goal not found"));
        if (StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(Request, currentUpdatedAt.Value) is { } pre)
            return StatusCode(pre.Status, pre.Body);
        try
        {
            var result = await _perf.UpdateGoalProgressAsync(employeeId.Value, role, id, body);
            await SetGoalETagAsync(id);
            return Ok(result);
        }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    private async Task SetGoalETagAsync(int goalId)
    {
        var updatedAt = await _perf.GetGoalUpdatedAtAsync(goalId);
        if (updatedAt != null)
            StaffDesk.API.Common.ConcurrencyHelper.SetETag(Response, updatedAt.Value);
    }

    private async Task<string> GetActorRoleAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return "";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "";
    }
}

[ApiController]
[Route("v1/feedback-notes")]
[Authorize]
public class FeedbackNotesController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public FeedbackNotesController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackNoteRequest body)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();
        try { return StatusCode(201, await _perf.CreateFeedbackNoteAsync(employeeId.Value, role, body)); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }

    private async Task<string> GetActorRoleAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return "";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "";
    }
}

[ApiController]
[Route("v1/competencies")]
[Authorize]
public class CompetenciesController : ApiControllerBase
{
    private readonly IPerformanceService _perf;

    public CompetenciesController(IPerformanceService perf, IUserRepository userRepository)
        : base(userRepository) => _perf = perf;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        // Competency library is reference data � allow Admin to read the catalogue (not reviews).
        // PM-38 is about performance data; Appendix C library is not a rating record.
        try { return Ok(await _perf.GetCompetenciesAsync()); }
        catch (TaskDomainException ex) { return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details)); }
    }
}
