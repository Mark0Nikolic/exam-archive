namespace ExamArchive.Models;

/// <summary>
/// A level of study, e.g. "Bachelor's" or "Master's". The top of the hierarchy.
/// </summary>
public class Studies
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // A level of study offers many majors.
    public ICollection<Major> Majors { get; set; } = new List<Major>();
}
