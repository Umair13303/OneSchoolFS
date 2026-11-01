using Microsoft.EntityFrameworkCore;

namespace SchoolManagement.FileServer.Data;

/// <summary>
/// Minimal DbContext for the FileServer. Points at the same SchoolDB used by
/// the Main API but only knows about the <c>FileStore</c> table — it does not
/// own the schema (the Main API owns migrations) and applies no global filters
/// so soft-deleted rows can still be served metadata about (the Controller
/// decides whether to honour <c>IsDeleted</c>).
/// </summary>
public class FileServerDbContext : DbContext
{
    public FileServerDbContext(DbContextOptions<FileServerDbContext> options) : base(options) { }

    public DbSet<FileStoreEntity> Files => Set<FileStoreEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileStoreEntity>(b =>
        {
            // Map to the table the Main API already created. EF Core pluralized
            // the DbSet name in AppDbContext (`FileStores => Set<FileStore>()`),
            // so the physical table is "FileStores" — match that exactly.
            b.ToTable("FileStores");
            b.HasKey(f => f.FileId);

            b.Property(f => f.OriginalName).IsRequired();
            b.Property(f => f.StoredName)  .IsRequired();
            b.Property(f => f.FilePath)    .IsRequired();
            // FileType, EntityType, EntityId, SizeBytes, UploadedBy, IsDeleted, UploadedAt
            // all use the Main API's column definitions — no overrides needed.
        });
    }
}
