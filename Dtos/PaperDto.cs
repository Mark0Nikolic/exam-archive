using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// An approved exam paper. Status is deliberately absent — this DTO is only ever
/// built from approved rows, so exposing the field would imply a filter the
/// browse API does not offer.
/// </summary>
/// <param name="PageCount">
/// How many files make up the paper. A listing needs this to show "4 pages"
/// without fetching the pages themselves, and a client needs it to know the
/// range of page numbers it may request.
/// </param>
public record PaperDto(
    int Id,
    ExamType ExamType,
    int Month,
    int Year,
    int PageCount,
    DateTime UploadedAt);
