namespace SchoolManagement.FileServer.Models;

/// <summary>
/// Returned to the caller after a successful upload. The Main API stores
/// <see cref="FileId"/> in whichever table the file belongs to
/// (e.g. <c>Students.PhotoFileId</c>, <c>Homework.FileId</c>).
/// </summary>
public class FileUploadResponse
{
    public int    FileId       { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl      { get; set; } = string.Empty;
    public string FileType     { get; set; } = string.Empty;
    public long   SizeBytes    { get; set; }
}
