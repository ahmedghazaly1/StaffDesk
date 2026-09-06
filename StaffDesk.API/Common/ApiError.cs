namespace StaffDesk.API.Common;

// Standard error envelope required by the SRS for all Task-related endpoints:
// { "error": { "code": "...", "message": "...", "details": [], "requestId": "guid" } }
public static class ApiError
{
    public static object Build(string code, string message, List<string>? details = null)
    {
        return new
        {
            error = new
            {
                code,
                message,
                details = details ?? new List<string>(),
                requestId = Guid.NewGuid().ToString()
            }
        };
    }
}
