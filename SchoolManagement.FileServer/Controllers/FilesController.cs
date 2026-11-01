using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.FileServer.Models;
using SchoolManagement.FileServer.Services;

namespace SchoolManagement.FileServer.Controllers;

/// <summary>
/// Endpoints listed in the project task doc, Module 8:
///   POST   /api/files/upload        → upload file, returns FileId
///   GET    /api/files/{fileId}      → stream/download file
///   GET    /api/files/{fileId}/info → file metadata
///   DELETE /api/files/{fileId}      → delete (SuperAdmin/Admin only)
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize] // Any authenticated scheme — JWT *or* X-Api-Key
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileStorageService storage, ILogger<FilesController> logger)
    {
        _storage = storage;
        _logger  = logger;
    }

    /// <summary>
    /// Upload a file. Multipart form: <c>file</c> (required), <c>entityType</c> (required),
    /// <c>entityId</c> (optional). <c>schoolName</c> + <c>label</c> are optional — when both
    /// are supplied together with <c>entityId</c>, the file is stored under a deterministic,
    /// extension-less name ("SchoolName-Label-EntityId") instead of a random one, and a
    /// re-upload for the same (entityType, entityId, label) supersedes the previous file.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(long.MaxValue)] // real cap lives in Program.cs Kestrel + service-level validation
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] FileUploadRequest req, CancellationToken ct)
    {
        var (file, entityType, entityId, schoolName, label) = (req.File, req.EntityType, req.EntityId, req.SchoolName, req.Label);

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "file is required and must not be empty." });
        if (string.IsNullOrWhiteSpace(entityType))
            return BadRequest(new { error = "entityType is required." });

        try
        {
            var uploadedBy = GetCurrentUserId();
            var result = await _storage.SaveAsync(file, entityType, entityId, uploadedBy, ct, schoolName, label);
            return Ok(result);
        }
        catch (ArgumentException ex)        { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex){ return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");
            return Problem("Upload failed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Stream/download a file.</summary>
    [HttpGet("{fileId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int fileId, CancellationToken ct)
    {
        var opened = await _storage.OpenAsync(fileId, ct);
        if (opened is null) return NotFound();
        var (stream, contentType, originalName) = opened.Value;

        // Named/deterministic files are stored with no extension; append one
        // guessed from the content-type so downloads still open correctly.
        var downloadName = Path.HasExtension(originalName)
            ? originalName
            : originalName + MimeExtensions.GuessExtension(contentType);

        // enableRangeProcessing lets browsers/video players resume + seek
        return File(stream, contentType, fileDownloadName: downloadName, enableRangeProcessing: true);
    }

    /// <summary>Return metadata about a file.</summary>
    [HttpGet("{fileId:int}/info")]
    [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Info(int fileId, CancellationToken ct)
    {
        var info = await _storage.GetInfoAsync(fileId, ct);
        return info is null ? NotFound() : Ok(info);
    }

    /// <summary>Soft-delete a file. SuperAdmin/Admin only.</summary>
    [HttpDelete("{fileId:int}")]
    [Authorize(Roles = "SuperAdmin,Admin,superadmin,admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int fileId, CancellationToken ct)
    {
        var ok = await _storage.SoftDeleteAsync(fileId, GetCurrentUserId(), ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Pulls the user id from the JWT <c>sub</c>/<c>NameIdentifier</c> claim. Returns null for service-account calls.</summary>
    private int? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return int.TryParse(sub, out var id) ? id : (int?)null;
    }
}
