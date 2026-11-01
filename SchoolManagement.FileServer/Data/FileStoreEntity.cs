namespace SchoolManagement.FileServer.Data;

/// <summary>
/// EF Core entity mapped to the <c>FileStore</c> table in <c>SchoolDB</c>.
/// The same table is owned by the Main API (which already ran the migration);
/// here we just need a local POCO so we can insert/read rows without taking
/// a compile-time dependency on the Main API project.
/// </summary>
public class FileStoreEntity
{
    public int      FileId       { get; set; }
    public string   OriginalName { get; set; } = string.Empty;
    public string   StoredName   { get; set; } = string.Empty;
    public string   FilePath     { get; set; } = string.Empty;
    public string?  FileType     { get; set; }
    public long?    SizeBytes    { get; set; }
    public string?  EntityType   { get; set; }
    public int?     EntityId     { get; set; }
    public string?  Label        { get; set; }
    public bool     IsDeleted    { get; set; }
    public int?     UploadedBy   { get; set; }
    public DateTime UploadedAt   { get; set; } = DateTime.UtcNow;
}
