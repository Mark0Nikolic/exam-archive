using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// A paper as it appears in the moderation queue.
/// </summary>
/// <remarks>
/// Deliberately richer than <see cref="PaperDto"/>: a moderator is deciding
/// whether to publish this row, so they need the subject spelled out rather than
/// an id, and they need the status that <see cref="PaperDto"/> omits precisely
/// because the browse API only ever serves approved papers.
/// </remarks>
public record ModerationPaperDto(
    int Id,
    int SubjectId,
    string SubjectName,
    string ExamType,
    int Month,
    int Year,
    string FilePath,
    DateTime UploadedAt,
    PaperStatus Status);
