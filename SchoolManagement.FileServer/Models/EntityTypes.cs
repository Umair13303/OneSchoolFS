namespace SchoolManagement.FileServer.Models;

/// <summary>
/// String constants used in the <c>EntityType</c> column of <c>FileStore</c>.
/// These tell the FileServer which subfolder of <c>Storage/</c> a file belongs
/// in, and let the validator apply per-kind size/extension limits.
/// </summary>
public static class EntityTypes
{
    public const string Image                = "image";
    public const string Document             = "document";
    public const string Homework             = "homework";
    public const string HomeworkSubmission   = "homework-submission";
    public const string StudentPhoto         = "student-photo";
    public const string BirthCertificate     = "birth-certificate";
    public const string PrevSchoolCert       = "prev-school-cert";
    public const string StaffPhoto           = "staff-photo";
    public const string StaffDocument        = "staff-document";

    /// <summary>All accepted entity types — anything outside this set is rejected.</summary>
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Image, Document, Homework, HomeworkSubmission,
        StudentPhoto, BirthCertificate, PrevSchoolCert,
        StaffPhoto, StaffDocument
    };

    /// <summary>
    /// Top-level subfolder under <c>Storage/</c> for each entity type.
    /// The task doc lists images/ documents/ homework/, so everything else
    /// goes under one of those buckets.
    /// </summary>
    public static string ToFolder(string entityType) => entityType?.ToLowerInvariant() switch
    {
        Image or StudentPhoto or StaffPhoto                 => "images",
        Document or BirthCertificate or PrevSchoolCert
            or StaffDocument                                => "documents",
        Homework or HomeworkSubmission                     => "homework",
        _                                                  => "documents"
    };
}
