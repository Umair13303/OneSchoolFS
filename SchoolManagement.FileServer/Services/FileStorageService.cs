using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FileServerOptions = SchoolManagement.FileServer.Configuration.FileServerOptions;
using SchoolManagement.FileServer.Data;
using SchoolManagement.FileServer.Models;

namespace SchoolManagement.FileServer.Services;

/// <summary>
/// Disk-backed implementation. Saves files to
/// <c>{StorageRoot}/{bucket}/{yyyy}/{MM}/{GUID}{ext}</c>, records the row in
/// <c>FileStore</c>, and returns a <see cref="FileUploadResponse"/> with a
/// public download URL.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly FileServerDbContext _db;
    private readonly FileServerOptions  _opts;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _storageRoot;

    public FileStorageService(
        FileServerDbContext db,
        IOptions<FileServerOptions> opts,
        IWebHostEnvironment env,
        ILogger<FileStorageService> logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;

        _storageRoot = Path.IsPathRooted(_opts.StorageRoot)
            ? _opts.StorageRoot
            : Path.Combine(env.ContentRootPath, _opts.StorageRoot);

        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<FileUploadResponse> SaveAsync(
        IFormFile file, string entityType, int? entityId, int? uploadedBy, CancellationToken ct,
        string? schoolName = null, string? label = null)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File is empty.", nameof(file));

        entityType = (entityType ?? string.Empty).Trim().ToLowerInvariant();
        if (!EntityTypes.Allowed.Contains(entityType))
            throw new ArgumentException($"Unknown entityType '{entityType}'. Allowed: {string.Join(", ", EntityTypes.Allowed)}");

        ValidateSize(file.Length, entityType);
        var ext = Path.GetExtension(file.FileName) ?? string.Empty;
        ValidateExtension(ext, entityType);

        var bucket = EntityTypes.ToFolder(entityType);
        var now    = DateTime.UtcNow;

        // Named/deterministic path: only available once the owning record's id is
        // known (edit/re-upload flows). New-entity admission flows upload before
        // the id exists, so they fall through to the legacy GUID path below.
        var useNamedPath = entityId.HasValue && !string.IsNullOrWhiteSpace(schoolName);

        string relPath, absPath, displayName;
        FileStoreEntity? supersedes = null;

        if (useNamedPath)
        {
            var id = entityId!.Value;
            var effectiveLabel = string.IsNullOrWhiteSpace(label) ? entityType : label!.Trim();
            displayName = SanitizeBaseName($"{schoolName}-{effectiveLabel}-{id}");

            var absFolder = Path.Combine(_storageRoot, bucket);
            Directory.CreateDirectory(absFolder);

            relPath = Path.Combine(bucket, displayName).Replace('\\', '/');
            absPath = Path.Combine(absFolder, displayName);

            // A prior upload of the same (entityType, entityId, label) supersedes here.
            supersedes = await _db.Files.FirstOrDefaultAsync(f =>
                !f.IsDeleted && f.EntityType == entityType && f.EntityId == id &&
                f.Label == effectiveLabel, ct);

            await using (var fs = new FileStream(
                             absPath, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            {
                await file.CopyToAsync(fs, ct);
            }

            if (supersedes is not null)
            {
                supersedes.IsDeleted = true;
            }
        }
        else
        {
            // Layout: Storage/{bucket}/{yyyy}/{MM}/{GUID}{ext}
            var relFolder = Path.Combine(bucket, now.ToString("yyyy"), now.ToString("MM"));
            var absFolder = Path.Combine(_storageRoot, relFolder);
            Directory.CreateDirectory(absFolder);

            var storedName = $"{Guid.NewGuid():N}{ext}";
            relPath = Path.Combine(relFolder, storedName).Replace('\\', '/');
            absPath = Path.Combine(absFolder, storedName);
            displayName = SanitizeName(file.FileName);

            await using var fs = new FileStream(
                absPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);
            await file.CopyToAsync(fs, ct);
        }

        var entity = new FileStoreEntity
        {
            OriginalName = displayName,
            StoredName   = Path.GetFileName(absPath),
            FilePath     = relPath,            // store RELATIVE path, never absolute
            FileType     = file.ContentType,
            SizeBytes    = file.Length,
            EntityType   = entityType,
            EntityId     = entityId,
            Label        = useNamedPath ? (string.IsNullOrWhiteSpace(label) ? entityType : label!.Trim()) : null,
            UploadedBy   = uploadedBy,
            UploadedAt   = now,
            IsDeleted    = false
        };

        _db.Files.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Best-effort cleanup if the row couldn't be persisted.
            TryDelete(absPath);
            throw;
        }

        return new FileUploadResponse
        {
            FileId       = entity.FileId,
            OriginalName = entity.OriginalName,
            FileType     = entity.FileType ?? string.Empty,
            SizeBytes    = entity.SizeBytes ?? 0,
            FileUrl      = BuildPublicUrl(entity.FileId)
        };
    }

    public async Task<(Stream Stream, string ContentType, string OriginalName)?> OpenAsync(int fileId, CancellationToken ct)
    {
        var row = await _db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FileId == fileId && !f.IsDeleted, ct);
        if (row is null) return null;

        var abs = Path.Combine(_storageRoot, row.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(abs))
        {
            _logger.LogWarning("FileStore row {FileId} points at missing file {Path}", fileId, abs);
            return null;
        }

        var stream = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return (stream, row.FileType ?? "application/octet-stream", row.OriginalName);
    }

    public async Task<FileInfoResponse?> GetInfoAsync(int fileId, CancellationToken ct)
    {
        var row = await _db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FileId == fileId && !f.IsDeleted, ct);
        if (row is null) return null;

        return new FileInfoResponse
        {
            FileId       = row.FileId,
            OriginalName = row.OriginalName,
            FileType     = row.FileType,
            SizeBytes    = row.SizeBytes,
            EntityType   = row.EntityType,
            EntityId     = row.EntityId,
            UploadedBy   = row.UploadedBy,
            UploadedAt   = row.UploadedAt,
            FileUrl      = BuildPublicUrl(row.FileId)
        };
    }

    public async Task<bool> SoftDeleteAsync(int fileId, int? deletedBy, CancellationToken ct)
    {
        var row = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId && !f.IsDeleted, ct);
        if (row is null) return false;

        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "File {FileId} soft-deleted by user {UserId}. Physical file kept on disk at {Path}.",
            fileId, deletedBy, row.FilePath);
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private string BuildPublicUrl(int fileId)
    {
        var baseUrl = (_opts.BaseUrl ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/api/files/{fileId}";
    }

    private void ValidateSize(long size, string entityType)
    {
        var limit = _opts.MaxSizeBytesPerEntity.TryGetValue(entityType, out var perType)
            ? perType
            : _opts.MaxUploadBytes;
        if (size > limit)
            throw new InvalidOperationException(
                $"File ({size:n0} bytes) exceeds the limit of {limit:n0} bytes for entityType '{entityType}'.");
    }

    private void ValidateExtension(string ext, string entityType)
    {
        if (!_opts.AllowedExtensions.TryGetValue(entityType, out var allowed) || allowed.Length == 0)
            return; // no per-entity allow-list means "allow all"
        var normalized = ext.TrimStart('.').ToLowerInvariant();
        var ok = allowed.Any(x => string.Equals(x.TrimStart('.'), normalized, StringComparison.OrdinalIgnoreCase));
        if (!ok)
            throw new InvalidOperationException(
                $"File extension '.{normalized}' is not allowed for entityType '{entityType}'. Allowed: {string.Join(", ", allowed)}");
    }

    /// <summary>Strip directory parts and keep a safe filename for display.</summary>
    private static string SanitizeName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name)) name = "upload";
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) name = name.Replace(c, '_');
        return name.Length > 500 ? name[..500] : name;
    }

    /// <summary>
    /// Builds a deterministic, extension-less filename like "GreenValley-Degree-104"
    /// from free-text pieces (school name, document label, person id). Strips
    /// anything that isn't a letter, digit, space, or dash, then collapses spaces
    /// to single dashes so the result is a safe, readable filename on any OS.
    /// </summary>
    private static string SanitizeBaseName(string raw)
    {
        var cleaned = new string((raw ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray());
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned.Trim(), @"[\s_]+", "-");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "-{2,}", "-");
        return string.IsNullOrWhiteSpace(cleaned) ? $"file-{Guid.NewGuid():N}" : cleaned;
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to clean up orphaned file {Path}", path); }
    }
}
