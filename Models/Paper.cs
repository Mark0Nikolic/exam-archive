namespace ExamArchive.Models;

/// <summary>
/// An uploaded exam paper belonging to a single <see cref="Subject"/>.
/// </summary>
public class Paper
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    /// <summary>
    /// The paper's pages, in reading order. A scan is a single PDF; a photographed
    /// paper is one image per page.
    /// </summary>
    public List<PaperFile> Files { get; set; } = [];

    /// <summary>Which sitting this paper is from. Stored as text and enforced by a check constraint.</summary>
    public ExamType ExamType { get; set; }

    /// <summary>Month the exam was held, 1-12.</summary>
    public int Month { get; set; }

    public int Year { get; set; }

    /// <summary>Left at default so SQLite fills it in with CURRENT_TIMESTAMP on insert.</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>Moderation state. Stored as text and defaulted to Pending in the database.</summary>
    public PaperStatus Status { get; set; } = PaperStatus.Pending;
}
