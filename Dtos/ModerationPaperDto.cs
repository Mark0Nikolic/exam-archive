using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// A paper as it appears in the review queue. Carries more than
/// <see cref="PaperDto"/> because a moderator is judging the submission itself,
/// not browsing an archive: the subject name saves a lookup per row, and the
/// status is the field being acted on.
/// </summary>
public record ModerationPaperDto(
    int Id,
    int SubjectId,
    string SubjectName,
    ExamType ExamType,
    int Month,
    int Year,
    int PageCount,
    DateTime UploadedAt,
    PaperStatus Status);
