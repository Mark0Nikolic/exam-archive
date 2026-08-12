namespace ExamArchive.Models;

/// <summary>
/// A course/subject. The same subject can be taught in several majors,
/// potentially in a different year of study for each.
/// </summary>
public class Subject
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Majors are reached through the join entity, which carries YearOfStudy.
    public ICollection<MajorSubject> MajorSubjects { get; set; } = new List<MajorSubject>();

    public ICollection<Paper> Papers { get; set; } = new List<Paper>();
}
