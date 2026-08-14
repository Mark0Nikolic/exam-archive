using System.Security.Claims;

namespace ExamArchive.Services;

/// <summary>
/// Reads the application's own claims back off a signed-in principal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in account's id, or null when the caller is anonymous.
    /// </summary>
    /// <remarks>
    /// Null rather than throwing, because most callers are endpoints that work
    /// either way: an upload is accepted from anyone, and being signed in only
    /// changes whether it is recorded against an account.
    /// <para>
    /// The parse is defensive against a claim that is present but not a number.
    /// That cannot happen from <see cref="UserAccountService.BuildPrincipal"/>, but
    /// the value arrives inside a cookie the server issued and later re-read, and
    /// an id is about to be written into a foreign key column.
    /// </para>
    /// </remarks>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var id) ? id : null;
    }
}
