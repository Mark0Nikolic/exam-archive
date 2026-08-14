namespace ExamArchive.Models;

/// <summary>
/// A course/subject. The same subject can be taught in several majors,
/// potentially in a different year of study for each.
/// </summary>
public class Subject
{
    public int Id { get; set; }

    /// <summary>
    /// The university's course code, e.g. "IT230". Unique where present.
    /// </summary>
    /// <remarks>
    /// Nullable because the archive can be populated before every code is known,
    /// and inventing one would put fiction in the database. Names alone are not
    /// dependable identifiers — two majors can each teach a different course
    /// called "Programming Fundamentals" — so the code is what disambiguates them
    /// and what students actually search by.
    /// </remarks>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    // Majors are reached through the join entity, which carries YearOfStudy.
    public ICollection<MajorSubject> MajorSubjects { get; set; } = new List<MajorSubject>();

    public ICollection<Paper> Papers { get; set; } = new List<Paper>();
}
