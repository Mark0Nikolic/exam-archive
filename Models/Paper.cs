namespace ExamArchive.Models;

/// <summary>
/// An uploaded exam paper belonging to a single <see cref="Subject"/>.
/// </summary>
public class Paper
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public string FilePath { get; set; } = string.Empty;

    /// <summary>"Midterm", "Final" or "Resit" — enforced by a check constraint.</summary>
    public string ExamType { get; set; } = string.Empty;

    /// <summary>Month the exam was held, 1-12.</summary>
    public int Month { get; set; }

    public int Year { get; set; }

    /// <summary>Left at default so SQLite fills it in with CURRENT_TIMESTAMP on insert.</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>"Pending", "Approved" or "Rejected". Defaults to "Pending" in the database.</summary>
    public string Status { get; set; } = "Pending";
}
