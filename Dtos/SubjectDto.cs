namespace ExamArchive.Dtos;

/// <summary>
/// A subject as taught within one particular major. <see cref="YearOfStudy"/>
/// comes from the MajorSubject junction row, so the same subject can appear
/// with a different year depending on which major was requested.
/// </summary>
/// <param name="Code">
/// The university's course code, or null where it is not recorded. Clients
/// should treat it as a label to show beside the name, not as an identifier to
/// address the subject by — that is what <paramref name="Id"/> is for.
/// </param>
public record SubjectDto(int Id, string? Code, string Name, int YearOfStudy);
