using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StaffDesk.API.Common;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.API.Middleware;

// Part F - PL-1/PL-2/PL-3: a single chokepoint for every creating POST, so no controller has to
// remember to implement this itself. A request replayed with the same Idempotency-Key and the
// same body gets back the original response verbatim, without re-executing; the same key reused
// with a different body is rejected outright (a client bug, not a legitimate retry).
public class IdempotencyMiddleware
{
    private static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db, IUserRepository userRepository)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            await _next(context);
            return;
        }

        var callerId = await GetCallerIdAsync(context, userRepository);
        if (callerId == null)
        {
            // Unauthenticated request - [Authorize] will reject it downstream; nothing to key on here.
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var requestHash = ComputeHash($"{context.Request.Method}:{context.Request.Path}:{body}");

        var existing = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.CallerId == callerId.Value && r.Key == key);

        if (existing != null && existing.ExpiresAt < DateTime.UtcNow)
        {
            db.IdempotencyRecords.Remove(existing);
            await db.SaveChangesAsync();
            existing = null;
        }

        if (existing != null)
        {
            if (existing.RequestHash != requestHash)
            {
                context.Response.StatusCode = 422;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiError.Build(
                    TaskErrorCodes.IdempotencyKeyConflict,
                    "This Idempotency-Key was already used with a different request. Use a new key for a different request.",
                    new List<string> { $"field:Idempotency-Key" }));
                return;
            }

            // PL-1: replay the original response without re-executing the handler.
            context.Response.StatusCode = existing.ResponseStatusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Idempotency-Replayed"] = "true";
            await context.Response.WriteAsync(existing.ResponseBodyJson);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        context.Response.Body = originalBody;
        buffer.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody);

        // Every outcome is stored, not just success - a stable 4xx business error is still a
        // correct answer to replay for the same bad input, and storing it stops a retry loop from
        // hammering the handler again for a request that will never succeed.
        try
        {
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                CallerId = callerId.Value,
                Key = key!,
                RequestHash = requestHash,
                ResponseStatusCode = context.Response.StatusCode,
                ResponseBodyJson = responseText,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(RecordLifetime)
            });
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Two concurrent requests with the same key raced here; the unique (CallerId, Key)
            // index means only one insert wins. That's fine - the response already went out.
            _logger.LogWarning(ex, "Idempotency record for key {Key} was not stored (likely a concurrent duplicate)", key);
        }
    }

    private static async Task<int?> GetCallerIdAsync(HttpContext context, IUserRepository userRepository)
    {
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return null;

        // Scoped to the authenticated principal (User.Id), not the resolved Employee - stable even
        // for accounts with no linked employee, and this is who "the caller" actually is (SEC-3).
        var user = await userRepository.GetByIdAsync(userId);
        return user?.Id;
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
