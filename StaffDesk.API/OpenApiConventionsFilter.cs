using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StaffDesk.API;

/// <summary>NFR-21: document error responses and concurrency/rate-limit headers on every operation.</summary>
public sealed class OpenApiConventionsFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
        operation.Responses.TryAdd("429", new OpenApiResponse
        {
            Description = "Rate limited. Response includes Retry-After, X-RateLimit-Limit, X-RateLimit-Remaining."
        });

        operation.Parameters ??= new List<OpenApiParameter>();
        if (!operation.Parameters.Any(p => p.Name == "X-Request-Id"))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Request-Id",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional client request id; echoed on the response."
            });
        }

        var method = context.ApiDescription.HttpMethod ?? "";
        if (method is "PUT" or "PATCH")
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = false,
                Description = "ETag from GET. Required on task/employee/review/goal updates (PL-5). 412 includes currentETag."
            });
            operation.Responses.TryAdd("412", new OpenApiResponse { Description = "Precondition Failed — stale ETag. Body includes currentETag." });
            operation.Responses.TryAdd("428", new OpenApiResponse { Description = "Precondition Required — If-Match missing." });
        }

        if (method == "POST")
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Required for creating POSTs (PL-1). Replay returns the stored response; mismatch returns 422."
            });
        }
    }
}
