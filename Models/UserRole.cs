namespace ExamArchive.Models;

/// <summary>
/// What a staff account is allowed to do. One role per account, deliberately.
/// </summary>
/// <remarks>
/// Every account in this system is staff. Students never sign in: uploading is
/// anonymous and browsing is public, so an account for them would gate a door that
/// is deliberately open. What a submitter gets instead of an account is a claim
/// code — see <see cref="Services.ClaimToken"/>.
/// <para>
/// A set of roles per user is the more flexible design, and it is the wrong one
/// here: the two levels are strictly nested, so a user holding both says nothing
/// that Admin does not already say. A single column also lets the database enforce
/// the value with a check constraint, which a join table cannot.
/// </para>
/// <para>
/// Stored as text for the same reason as <see cref="PaperStatus"/> — roles have no
/// natural order, so a number would be an arbitrary code that has to be looked up
/// every time someone reads the table by eye.
/// </para>
/// </remarks>
public enum UserRole
{
    /// <summary>
    /// Reviews the submission queue: approve, reject, and publish directly. The
    /// operational role — it acts on papers moving through the archive.
    /// </summary>
    Moderator,

    /// <summary>
    /// Everything a moderator can do, plus the structural work: managing accounts
    /// and the subject catalogue that papers are filed against.
    /// </summary>
    /// <remarks>
    /// The line between the two is what each one edits. A moderator judges the
    /// papers; an admin edits the things papers are judged against, where a mistake
    /// affects the whole archive rather than one submission.
    /// </remarks>
    Admin
}
