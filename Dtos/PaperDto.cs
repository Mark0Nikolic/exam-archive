namespace ExamArchive.Dtos;

/// <summary>
/// An approved exam paper. Status is deliberately absent — this DTO is only ever
/// built from approved rows, so exposing the field would imply a filter the
/// browse API does not offer.
/// </summary>
public record PaperDto(
    int Id,
    string ExamType,
    int Month,
    int Year,
    string FilePath,
    DateTime UploadedAt);
