namespace ExamArchive.Dtos;

/// <summary>
/// One page of a paper, as described to a client deciding what to fetch.
/// </summary>
/// <remarks>
/// The stored path is deliberately absent. It is a server-side filesystem
/// location, and publishing it would invite clients to construct their own
/// paths — the exact thing the traversal guard on download exists to stop.
/// Pages are addressed by number instead.
/// </remarks>
public record PaperFileDto(
    int PageNumber,
    string ContentType,
    long SizeBytes);
