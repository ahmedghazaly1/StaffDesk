using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Filters;

/// <summary>
/// SP-7: field-level redaction in one serialisation pass — leave type (CP-8),
/// performance fields on generic payloads (PM-7), audit changesJson except AUDITOR.
/// </summary>
public class FieldRedactionResultFilter : IAsyncResultFilter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private static readonly HashSet<string> PerformanceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "selfOverallRating", "managerOverallRating", "calibratedOverallRating",
        "selfJustification", "managerJustification", "selfScoresJson", "managerScoresJson",
        "overallRating", "justification"
    };

    private readonly IUserRepository _users;

    public FieldRedactionResultFilter(IUserRepository users) => _users = users;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result
            && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? employeeId = null;
            var role = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            if (int.TryParse(claim, out var userId))
            {
                var user = await _users.GetByIdAsync(userId);
                employeeId = user?.EmployeeId;
                role = user?.Role ?? role;
            }

            var path = context.HttpContext.Request.Path.Value ?? "";
            var node = JsonSerializer.SerializeToNode(result.Value, JsonOpts);
            if (node != null)
            {
                Redact(node, employeeId, role, path);
                result.Value = JsonSerializer.Deserialize<object>(node.ToJsonString(), JsonOpts);
            }
        }

        await next();
    }

    private static void Redact(JsonNode node, int? actorEmployeeId, string role, string path)
    {
        if (node is JsonArray arr)
        {
            foreach (var child in arr)
                if (child != null) Redact(child, actorEmployeeId, role, path);
            return;
        }

        if (node is not JsonObject obj) return;

        foreach (var child in obj.ToList())
            if (child.Value != null) Redact(child.Value, actorEmployeeId, role, path);

        var isLeave = obj.ContainsKey("startDate") && obj.ContainsKey("endDate")
                      && (obj.ContainsKey("type") || obj.ContainsKey("employeeId"));
        if (isLeave && obj.ContainsKey("employeeId"))
        {
            var subjectId = obj["employeeId"] is JsonValue jv && jv.TryGetValue<int>(out var sid)
                ? sid
                : (int?)null;
            var maySeeType = role == User.Roles.HrAdmin
                             || (subjectId.HasValue && actorEmployeeId == subjectId.Value);
            if (!maySeeType)
            {
                if (obj.ContainsKey("type")) obj["type"] = null;
                if (obj.ContainsKey("note")) obj["note"] = null;
                obj["availabilityOnly"] = true;
            }
        }

        if (obj.ContainsKey("changesJson") && role != User.Roles.Auditor)
            obj["changesJson"] = null;

        var genericEmployeeSurface = path.Contains("/employees", StringComparison.OrdinalIgnoreCase)
                                     && !path.Contains("/reviews", StringComparison.OrdinalIgnoreCase)
                                     && !path.Contains("/performance", StringComparison.OrdinalIgnoreCase);
        if (genericEmployeeSurface)
        {
            foreach (var key in PerformanceKeys)
                if (obj.ContainsKey(key)) obj.Remove(key);
        }
    }
}
