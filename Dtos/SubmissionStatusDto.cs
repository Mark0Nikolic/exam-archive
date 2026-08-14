using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// What became of an anonymous submission, looked up by its claim code.
/// </summary>
/// <remarks>
/// Deliberately thin. The caller has proved only that they hold the code, which is
/// consistent with being the submitter and with having found the code written on a
/// desk — so this carries what the submitter needs to act on and nothing that would
/// be worth harvesting. No file list, no stored paths, and nothing about the
/// moderator who decided.
/// </remarks>
/// <param name="RejectionReason">
/// Why it was turned down, in the moderator's own words. Null unless rejected, and
/// the reason this endpoint exists at all.
/// </param>
public record SubmissionStatusDto(
    int Id,
    string SubjectName,
    ExamType ExamType,
    int Month,
    int Year,
    DateTime UploadedAt,
    PaperStatus Status,
    DateTime? ReviewedAt,
    string? RejectionReason);
