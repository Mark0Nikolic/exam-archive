namespace ExamArchive.Dtos;

/// <summary>
/// A major as returned by the browse API. Flat by design — no navigation
/// properties, so the serializer can never walk back into Studies or Subject.
/// </summary>
public record MajorDto(int Id, string Name, int StudiesId);
