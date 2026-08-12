namespace ExamArchive.Dtos;

/// <summary>
/// A subject as taught within one particular major. <see cref="YearOfStudy"/>
/// comes from the MajorSubject junction row, so the same subject can appear
/// with a different year depending on which major was requested.
/// </summary>
public record SubjectDto(int Id, string Name, int YearOfStudy);
