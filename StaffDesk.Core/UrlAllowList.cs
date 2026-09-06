namespace StaffDesk.Core;

/// <summary>SP-9: only http(s) URLs; http is limited to loopback.</summary>
public static class UrlAllowList
{
    public static readonly string[] AllowedSchemes = { "https", "http" };

    public static bool IsAllowed(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            return false;
        return uri.IsLoopback;
    }
}
