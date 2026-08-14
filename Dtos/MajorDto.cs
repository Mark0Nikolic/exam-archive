namespace ExamArchive.Dtos;

/// <summary>
/// A major as returned by the browse API. Flat by design — no navigation
/// properties, so the serializer can never walk back into Studies or Subject.
/// </summary>
/// <param name="NameSr">
/// The Serbian name, in Cyrillic. Always present. A client showing Latin
/// transliterates this rather than asking the server for it — the conversion is
/// mechanical in that direction, and doing it client-side keeps the language
/// switch instant instead of a round trip.
/// </param>
/// <param name="NameEn">
/// The English name, or null where none is recorded. Clients fall back to
/// <paramref name="NameSr"/> rather than showing a blank.
/// </param>
public record MajorDto(int Id, string NameSr, string? NameEn, int StudiesId);
