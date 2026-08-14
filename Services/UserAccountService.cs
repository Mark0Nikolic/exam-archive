using System.Security.Claims;
using ExamArchive.Data;
using ExamArchive.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Services;

/// <summary>
/// Hashes passwords, checks them, and turns an account into the identity the rest
/// of the framework understands.
/// </summary>
/// <remarks>
/// The hashing is <see cref="PasswordHasher{TUser}"/> straight out of ASP.NET Core
/// Identity, used without the rest of Identity. It is PBKDF2-HMAC-SHA512 with a
/// per-password random salt and a high iteration count, and it writes a version
/// marker into every hash so the iteration count can be raised later without
/// invalidating anybody's password — see <see cref="ValidateCredentialsAsync"/>.
/// <para>
/// Writing this by hand is the classic way to get it wrong: a bare SHA-256, no
/// salt, or a comparison with <c>==</c> that leaks the answer through its timing.
/// None of that is worth reinventing for a class that ships in the framework.
/// </para>
/// </remarks>
public sealed class UserAccountService
{
    /// <summary>
    /// A real hash, verified against when no such user exists.
    /// </summary>
    /// <remarks>
    /// Without this, a login for an unknown username returns as fast as the
    /// database lookup, while a known username costs the full PBKDF2 work. That
    /// difference is measurable over a network and turns the login endpoint into a
    /// way to enumerate valid accounts. Verifying a throwaway hash makes both paths
    /// do the same work.
    /// </remarks>
    private static readonly string DecoyHash =
        new PasswordHasher<User>().HashPassword(new User(), "not-a-real-password");

    private readonly ExamArchiveDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(ExamArchiveDbContext db, ILogger<UserAccountService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Produces the stored form of a password.</summary>
    public string HashPassword(User user, string password) =>
        _hasher.HashPassword(user, password);

    /// <summary>
    /// Checks a username and password, returning the account on success and null
    /// on any failure.
    /// </summary>
    /// <remarks>
    /// One null for every reason — no such user, wrong password, deactivated
    /// account — because the caller turns it into one message. Telling an
    /// unauthenticated caller which of those it was hands them a way to confirm
    /// that an account exists.
    /// </remarks>
    public async Task<User?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        // Tracked, not AsNoTracking: a successful login may rewrite the hash below.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            // Burn the same CPU the real path would, then fail. The result is
            // discarded; the point is the time it took.
            _hasher.VerifyHashedPassword(new User(), DecoyHash, password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Checked after the password, not before. Short-circuiting on a disabled
        // account would answer faster than a wrong password does, which is the same
        // timing leak the decoy above exists to close.
        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Sign-in refused for deactivated account {Username}.", user.Username);
            return null;
        }

        // The password is right but was hashed by an older, weaker configuration.
        // This is the moment it can be upgraded: the plaintext is in hand exactly
        // once, here. Doing nothing would leave old accounts on old parameters
        // forever.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Upgraded the stored password hash for {Username}.", user.Username);
        }

        return user;
    }

    /// <summary>
    /// Builds the identity that goes into the session cookie.
    /// </summary>
    /// <remarks>
    /// Only the id, the name and the role. Everything placed here is copied into
    /// the cookie and travels on every request, so it should be the few facts worth
    /// re-reading on each call rather than a snapshot of the account.
    /// <para>
    /// It is also a snapshot in the way that matters: a role changed in the database
    /// does not reach a cookie already issued. For an archive with a handful of
    /// staff that is fine — demoting someone takes effect when their cookie expires
    /// or they sign out. An application where that is not acceptable has to validate
    /// the principal against the database on each request.
    /// </para>
    /// </remarks>
    public static ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        // The scheme name has to match the one the cookie handler was registered
        // under, or the resulting principal is not considered authenticated.
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}
