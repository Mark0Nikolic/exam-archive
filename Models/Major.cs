namespace ExamArchive.Models;

/// <summary>
/// A course of study within a <see cref="Studies"/> level, e.g. "Рачунарске науке".
/// </summary>
public class Major
{
    public int Id { get; set; }

    /// <summary>The name in Serbian Cyrillic. Required — see <see cref="Studies.NameSr"/>.</summary>
    public string NameSr { get; set; } = string.Empty;

    /// <summary>The English name, or null if not supplied.</summary>
    public string? NameEn { get; set; }

    public int StudiesId { get; set; }

    public Studies? Studies { get; set; }

    // Subjects are reached through the join entity, which carries YearOfStudy.
    public ICollection<MajorSubject> MajorSubjects { get; set; } = new List<MajorSubject>();
}
