namespace SchoolManagement.FileServer.Models;

/// <summary>
/// Guesses a file extension from a MIME/content-type string. Used only to
/// build a friendly download filename for files stored without an extension
/// on disk — never used to validate uploads.
/// </summary>
public static class MimeExtensions
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"]    = ".jpg",
        ["image/png"]     = ".png",
        ["image/gif"]     = ".gif",
        ["image/webp"]    = ".webp",
        ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["text/plain"] = ".txt"
    };

    public static string GuessExtension(string? contentType)
        => contentType is not null && Map.TryGetValue(contentType, out var ext) ? ext : string.Empty;
}
