namespace ExamArchive.Models;

/// <summary>
/// A course of study within a <see cref="Studies"/> level, e.g. "Computer Science".
/// </summary>
public class Major
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StudiesId { get; set; }

    public Studies? Studies { get; set; }

    // Subjects are reached through the join entity, which carries YearOfStudy.
    public ICollection<MajorSubject> MajorSubjects { get; set; } = new List<MajorSubject>();
}
