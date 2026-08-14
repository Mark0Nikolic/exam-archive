using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// Receipt for a freshly uploaded paper. Unlike <see cref="PaperDto"/> this one
/// does carry Status: the whole point of the response is to tell the submitter
/// whether the paper is queued for review or already published.
/// </summary>
/// <param name="ClaimToken">
/// The code for checking on this submission later, or null when staff uploaded the
/// paper — they can see it in the queue and need no code.
/// </param>
/// <remarks>
/// This response is the only place the claim code ever appears. The server keeps
/// nothing but its hash, so it cannot be shown again, and a frontend that does not
/// put it in front of the submitter has silently thrown it away. That is worth
/// saying loudly in the UI rather than printing it in small text.
/// </remarks>
public record UploadedPaperDto(
    int Id,
    int SubjectId,
    ExamType ExamType,
    int Month,
    int Year,
    DateTime UploadedAt,
    PaperStatus Status,
    IReadOnlyList<PaperFileDto> Files,
    string? ClaimToken)
{
    /// <summary>
    /// Builds the receipt from a just-saved paper. Shared by the public and staff
    /// upload endpoints, which differ in the status they produce and in whether a
    /// claim code was issued.
    /// </summary>
    /// <param name="claimToken">
    /// Passed in rather than read from the paper, because the paper only ever holds
    /// the hash — this is the one moment the code itself is in memory.
    /// </param>
    public static UploadedPaperDto From(Paper paper, string? claimToken = null) => new(
        paper.Id,
        paper.SubjectId,
        paper.ExamType,
        paper.Month,
        paper.Year,
        paper.UploadedAt,
        paper.Status,
        [.. paper.Files
            .OrderBy(f => f.PageNumber)
            .Select(f => new PaperFileDto(f.PageNumber, f.ContentType, f.SizeBytes))],
        claimToken);
}
