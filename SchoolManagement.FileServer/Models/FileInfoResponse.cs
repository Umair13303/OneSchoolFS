namespace SchoolManagement.FileServer.Models;

/// <summary>
/// Metadata response for <c>GET /api/files/{id}/info</c>. We never return
/// the absolute disk path — only the public download URL.
/// </summary>
public class FileInfoResponse
{
    public int      FileId       { get; set; }
    public string   OriginalName { get; set; } = string.Empty;
    public string?  FileType     { get; set; }
    public long?    SizeBytes    { get; set; }
    public string?  EntityType   { get; set; }
    public int?     EntityId     { get; set; }
    public int?     UploadedBy   { get; set; }
    public DateTime UploadedAt   { get; set; }
    public string   FileUrl      { get; set; } = string.Empty;
}
