namespace ExamArchive.Dtos;

/// <summary>
/// A subject as taught within one particular major. <see cref="YearOfStudy"/>
/// comes from the MajorSubject junction row, so the same subject can appear
/// with a different year depending on which major was requested.
/// </summary>
/// <param name="Code">
/// The university's course code, or null where it is not recorded. The one
/// name-like field that reads the same in every language. Show it beside the
/// name; address the subject by <paramref name="Id"/>.
/// </param>
/// <param name="NameSr">The Serbian name, in Cyrillic. Always present.</param>
/// <param name="NameEn">The English name, or null. Fall back to Serbian.</param>
public record SubjectDto(
    int Id,
    string? Code,
    string NameSr,
    string? NameEn,
    int YearOfStudy);
