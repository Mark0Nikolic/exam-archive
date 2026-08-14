namespace ExamArchive.Models;

/// <summary>
/// An account that can sign in.
/// </summary>
/// <remarks>
/// Staff only. Students have no accounts — uploading is anonymous and browsing is
/// public, so the only people who sign in are the ones who review submissions and
/// the ones who configure the archive.
/// <para>
/// Hand-rolled rather than ASP.NET Core Identity. Identity brings seven tables,
/// two-factor, lockout, external logins, email confirmation and a whole schema
/// this application would carry without using — and the piece actually worth
/// having, its password hasher, is a standalone class that works perfectly well
/// on its own. See <see cref="Services.UserAccountService"/>.
/// </para>
/// <para>
/// There is no email address and no personal name. The archive never needs to
/// contact anyone, so storing a way to would be collecting personal data for no
/// purpose. Adding it later is one nullable column; un-collecting it is not.
/// </para>
/// </remarks>
public class User
{
    public int Id { get; set; }

    /// <summary>
    /// The login name. Unique, and compared case-insensitively so that a student
    /// typing "Marko" reaches the account created as "marko".
    /// </summary>
    /// <remarks>
    /// The case-insensitivity is a collation on the column rather than a second
    /// normalized copy of the string, which is how Identity does it. On SQLite that
    /// makes the unique index case-insensitive too, so the database refuses a
    /// second "MARKO" instead of trusting the application to check.
    /// </remarks>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The password, hashed. Never the password itself, and never reversible.
    /// </summary>
    /// <remarks>
    /// Format is whatever <c>PasswordHasher&lt;User&gt;</c> produces: a version
    /// marker, the salt, and the PBKDF2 output, all in one base64 string. The
    /// version marker is what lets the iteration count be raised later without
    /// invalidating existing passwords.
    /// </remarks>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Defaults to the lesser of the two, so an account created without an explicit
    /// role cannot come out as an administrator by omission.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Moderator;

    /// <summary>
    /// Whether the account may sign in. Checked at login, so revoking access is a
    /// flag rather than a delete.
    /// </summary>
    /// <remarks>
    /// Deleting is the wrong tool: a submitter's papers reference their account, and
    /// a moderator who leaves should stop being able to sign in without their past
    /// decisions becoming untraceable.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Papers this account published directly through the staff upload endpoint.
    /// Empty for staff who only review the queue.
    /// </summary>
    public List<Paper> Papers { get; set; } = [];
}
