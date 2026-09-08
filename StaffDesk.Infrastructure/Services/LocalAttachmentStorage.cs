using Microsoft.Extensions.Configuration;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Infrastructure.Services;

/// <summary>
/// Stores uploaded files on the local filesystem under a configurable root
/// ("Attachments:RootPath", default &lt;content root&gt;/App_Data/attachments).
/// Only the relative path is persisted, so the root can move between environments.
/// </summary>
public class LocalAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public LocalAttachmentStorage(IConfiguration configuration)
    {
        var configured = configuration["Attachments:RootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "attachments")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeFolder = SanitizeSegment(folder);
        var extension = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(safeFolder, storedName).Replace('\\', '/');

        var fullPath = ResolveFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, ct);

        return relativePath;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Stored attachment file is missing", storagePath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public bool Exists(string storagePath) => File.Exists(ResolveFullPath(storagePath));

    // Guards against a stored path ever escaping the root via traversal segments.
    private string ResolveFullPath(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Attachment path resolves outside the storage root");

        return combined;
    }

    private static string SanitizeSegment(string segment)
    {
        var cleaned = new string(segment.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrEmpty(cleaned) ? "misc" : cleaned;
    }
}
