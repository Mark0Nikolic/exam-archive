using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// Receipt for a freshly uploaded paper. Unlike <see cref="PaperDto"/> this one
/// does carry Status: the whole point of the response is to tell the submitter
/// whether the paper is queued for review or already published.
/// </summary>
public record UploadedPaperDto(
    int Id,
    int SubjectId,
    ExamType ExamType,
    int Month,
    int Year,
    DateTime UploadedAt,
    PaperStatus Status,
    IReadOnlyList<PaperFileDto> Files)
{
    /// <summary>
    /// Builds the receipt from a just-saved paper. Shared by the public and staff
    /// upload endpoints, which differ only in the status they produce.
    /// </summary>
    public static UploadedPaperDto From(Paper paper) => new(
        paper.Id,
        paper.SubjectId,
        paper.ExamType,
        paper.Month,
        paper.Year,
        paper.UploadedAt,
        paper.Status,
        [.. paper.Files
            .OrderBy(f => f.PageNumber)
            .Select(f => new PaperFileDto(f.PageNumber, f.ContentType, f.SizeBytes))]);
}
