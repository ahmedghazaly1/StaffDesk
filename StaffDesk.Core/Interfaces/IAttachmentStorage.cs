namespace StaffDesk.Core.Interfaces;

/// <summary>Binary storage for uploaded files. Paths returned here are relative to the storage root.</summary>
public interface IAttachmentStorage
{
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
    bool Exists(string storagePath);
}
