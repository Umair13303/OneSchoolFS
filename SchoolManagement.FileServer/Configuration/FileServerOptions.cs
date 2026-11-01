namespace SchoolManagement.FileServer.Configuration;

/// <summary>
/// Strongly-typed binding of the <c>FileServer</c> section in appsettings.json.
/// </summary>
public class FileServerOptions
{
    public const string SectionName = "FileServer";

    /// <summary>Public-facing base URL; used to build <c>FileUrl</c> in responses.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5002";

    /// <summary>
    /// Shared secret accepted in the <c>X-Api-Key</c> header. Used for
    /// server-to-server calls from the Main API.
    /// </summary>
    public string ApiKey  { get; set; } = string.Empty;

    /// <summary>Root folder for all uploaded files. Resolved relative to the app base directory if not absolute.</summary>
    public string StorageRoot { get; set; } = "Storage";

    /// <summary>Global max upload size in bytes (default 50 MB). Per-entity limits override this.</summary>
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>Per-entity-type extension allow-list.</summary>
    public Dictionary<string, string[]> AllowedExtensions { get; set; } = new();

    /// <summary>Per-entity-type max size override (bytes).</summary>
    public Dictionary<string, long> MaxSizeBytesPerEntity { get; set; } = new();
}
