namespace SchoolManagement.FileServer.Models;

/// <summary>
/// Multipart form body for <c>POST /api/files/upload</c>. Bound as a single
/// model (rather than individual [FromForm] parameters) because Swashbuckle
/// cannot generate a Swagger schema for an action that combines an IFormFile
/// parameter with other simple-type [FromForm] parameters — see
/// https://github.com/domaindrivendev/Swashbuckle.AspNetCore#handle-forms-and-file-uploads.
/// </summary>
public class FileUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? SchoolName { get; set; }
    public string? Label { get; set; }
}
