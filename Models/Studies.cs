namespace ExamArchive.Models;

/// <summary>
/// A level of study, e.g. "Основне академске студије" (Bachelor's). The top of
/// the hierarchy.
/// </summary>
public class Studies
{
    public int Id { get; set; }

    /// <summary>
    /// The name in Serbian, written in Cyrillic. Required.
    /// </summary>
    /// <remarks>
    /// Cyrillic is the stored form and Latin is generated from it in the browser,
    /// because that direction is the only one a machine can do reliably: њ is
    /// always "nj", but "nj" is one letter in "коњ" and two in "инјекција", and
    /// nothing short of a dictionary can tell those apart.
    /// </remarks>
    public string NameSr { get; set; } = string.Empty;

    /// <summary>
    /// The English name, or null where nobody has supplied one.
    /// </summary>
    /// <remarks>
    /// Optional because Serbian is the language this archive must work in; English
    /// is an accommodation. A client falls back to <see cref="NameSr"/> rather
    /// than showing a blank.
    /// </remarks>
    public string? NameEn { get; set; }

    // A level of study offers many majors.
    public ICollection<Major> Majors { get; set; } = new List<Major>();
}
