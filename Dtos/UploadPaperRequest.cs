using System.ComponentModel.DataAnnotations;

namespace ExamArchive.Dtos;

/// <summary>
/// The multipart form a submitter fills in to add a paper to the archive.
/// A class rather than a record because <see cref="IFormFile"/> only binds from
/// form fields, which needs settable properties.
/// </summary>
public class UploadPaperRequest
{
    [Required(ErrorMessage = "A file is required.")]
    public IFormFile? File { get; set; }

    /// <summary>The subject the paper belongs to. Must already exist.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "SubjectId must be a positive id.")]
    public int SubjectId { get; set; }

    /// <summary>"Midterm", "Final" or "Resit". Matched case-insensitively.</summary>
    [Required(ErrorMessage = "ExamType is required.")]
    public string ExamType { get; set; } = string.Empty;

    /// <summary>Month the exam was held, 1-12.</summary>
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    public int Month { get; set; }

    /// <summary>
    /// Year the exam was held. Bounds are checked in the controller rather than
    /// here — the upper bound is relative to today, which an attribute cannot express.
    /// </summary>
    public int Year { get; set; }
}
