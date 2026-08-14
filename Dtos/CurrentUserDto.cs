using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// Who the caller is signed in as.
/// </summary>
/// <remarks>
/// The client cannot read the session cookie — it is HttpOnly, which is the point
/// — so a React app reloading the page has no idea whether it is signed in until
/// it asks. This is the answer to that question, and it is what a frontend uses to
/// decide whether to render the moderation screens at all.
/// <para>
/// Convenience only: the server never trusts it. Hiding a button the caller may
/// not press is a courtesy, and the <c>[Authorize]</c> attribute is the actual
/// rule.
/// </para>
/// </remarks>
public record CurrentUserDto(int Id, string Username, UserRole Role);
