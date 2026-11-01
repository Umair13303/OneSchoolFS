using SchoolManagement.FileServer.Data;
using SchoolManagement.FileServer.Models;

namespace SchoolManagement.FileServer.Services;

/// <summary>
/// Contract for the FileServer storage layer. Implementations are responsible
/// for writing the bytes to disk, inserting a <see cref="FileStoreEntity"/>
/// row, and returning a <see cref="FileUploadResponse"/> the API can hand back
/// to callers.
/// </summary>
public interface IFileStorageService
{
    Task<FileUploadResponse> SaveAsync(
        IFormFile file,
        string entityType,
        int? entityId,
        int? uploadedBy,
        CancellationToken ct,
        string? schoolName = null,
        string? label = null);

    Task<(Stream Stream, string ContentType, string OriginalName)?> OpenAsync(int fileId, CancellationToken ct);

    Task<FileInfoResponse?> GetInfoAsync(int fileId, CancellationToken ct);

    Task<bool> SoftDeleteAsync(int fileId, int? deletedBy, CancellationToken ct);
}
