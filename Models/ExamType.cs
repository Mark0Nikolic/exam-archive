namespace ExamArchive.Models;

/// <summary>
/// Which sitting of a course an exam paper comes from.
/// </summary>
/// <remarks>
/// Stored in the database as the member name rather than its underlying number
/// — see the <c>HasConversion</c> call in <see cref="Data.ExamArchiveDbContext"/>
/// — so these names must keep matching the CK_Paper_ExamType check constraint.
/// Renaming a member without updating that constraint breaks every insert.
/// <para>
/// Text storage is safe here because exam types have no natural order: nothing
/// sorts by this column. <see cref="Paper.Month"/> is deliberately left an int
/// for the opposite reason.
/// </para>
/// </remarks>
public enum ExamType
{
    /// <summary>Sat partway through the course.</summary>
    Midterm,

    /// <summary>The main exam at the end of the course.</summary>
    Final,

    /// <summary>A repeat sitting for students who did not pass.</summary>
    Resit
}
