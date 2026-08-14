using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// Receipt for a freshly uploaded paper. Unlike <see cref="PaperDto"/> this one
/// does carry Status: the whole point of the response is to tell the submitter
/// the paper is queued for review rather than already published.
/// </summary>
public record UploadedPaperDto(
    int Id,
    int SubjectId,
    ExamType ExamType,
    int Month,
    int Year,
    DateTime UploadedAt,
    PaperStatus Status,
    IReadOnlyList<PaperFileDto> Files);
