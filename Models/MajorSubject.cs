namespace ExamArchive.Models;

/// <summary>
/// Join entity between <see cref="Major"/> and <see cref="Subject"/>.
/// Carries the year of study the subject is taught in for that particular major,
/// which is why it is an explicit entity rather than a skip navigation.
/// The primary key is the composite (MajorId, SubjectId) — configured in OnModelCreating.
/// </summary>
public class MajorSubject
{
    public int MajorId { get; set; }

    public Major? Major { get; set; }

    public int SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public int YearOfStudy { get; set; }
}
