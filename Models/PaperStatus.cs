namespace ExamArchive.Models;

/// <summary>
/// Where a paper sits in the moderation workflow.
/// </summary>
/// <remarks>
/// Stored in the database as the member name rather than its underlying number
/// — see the <c>HasConversion</c> call in <see cref="Data.ExamArchiveDbContext"/>
/// — so these names must keep matching the CK_Paper_Status check constraint.
/// Renaming a member without updating that constraint breaks every insert.
/// </remarks>
public enum PaperStatus
{
    /// <summary>Freshly uploaded, awaiting review. Deliberately unreachable from the browse API.</summary>
    Pending,

    /// <summary>Published: listed by the browse API and downloadable.</summary>
    Approved,

    /// <summary>Reviewed and turned down. Never served to the public.</summary>
    Rejected
}
