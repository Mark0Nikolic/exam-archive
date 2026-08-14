using System.Security.Claims;
using ExamArchive.Dtos;
using ExamArchive.Models;
using ExamArchive.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamArchive.Controllers;

/// <summary>
/// Signing in and out, and reporting who the caller is.
/// </summary>
/// <remarks>
/// Sessions are cookies, not bearer tokens. The deciding property is that a cookie
/// marked HttpOnly cannot be read by JavaScript, so a cross-site scripting flaw
/// anywhere in the frontend cannot walk off with a moderator's session — and this
/// application serves unreviewed, submitter-supplied files, which is a larger XSS
/// surface than most. A JWT held in localStorage is readable by any script on the
/// page by design.
/// <para>
/// The cost is that cookies are attached by the browser automatically, which is
/// what CSRF exploits; that is handled by the SameSite attribute set in Program.cs
/// and by the fact that every state-changing endpoint is a POST that will not
/// accept a form content type.
/// </para>
/// </remarks>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserAccountService _accounts;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserAccountService accounts, ILogger<AuthController> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    /// <summary>
    /// Signs in and issues the session cookie.
    /// </summary>
    /// <remarks>
    /// A frontend must send this with credentials included, and every later request
    /// too, or the browser will hold the cookie and never present it.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _accounts.ValidateCredentialsAsync(
            request.Username, request.Password, cancellationToken);

        if (user is null)
        {
            // One message for every failure. "No such user" and "wrong password"
            // are different facts, and telling them apart is how an attacker builds
            // a list of real accounts before spending any effort on passwords.
            _logger.LogInformation(
                "Failed sign-in attempt for {Username}.", request.Username);

            return Problem(
                title: "Sign-in failed",
                detail: "The username or password is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            UserAccountService.BuildPrincipal(user),
            new AuthenticationProperties
            {
                // Survives closing the browser. A moderator working through a queue
                // over several days should not have to sign in each morning, and the
                // sliding expiration configured on the handler ends the session on
                // inactivity rather than on a fixed clock.
                IsPersistent = true
            });

        _logger.LogInformation(
            "{Username} signed in as {Role}.", user.Username, user.Role);

        return Ok(new CurrentUserDto(user.Id, user.Username, user.Role));
    }

    /// <summary>
    /// Signs out, clearing the session cookie.
    /// </summary>
    /// <remarks>
    /// Anonymous rather than [Authorize] on purpose: signing out a session that has
    /// already expired should quietly succeed, not answer 401 and leave a frontend
    /// deciding what to do about a failure that does not matter.
    /// </remarks>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }

    /// <summary>
    /// Reports the signed-in account, or 401 if there is none.
    /// </summary>
    /// <remarks>
    /// The cookie is HttpOnly, so a page that has just loaded cannot inspect it to
    /// find out whether it holds a session. This is how it asks.
    /// </remarks>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserDto> Me()
    {
        var id = User.GetUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);

        // [Authorize] only proves a principal exists, not that it carries the
        // claims this application put there. A cookie issued by an older version of
        // the sign-in code would satisfy it and still be missing one of these, so
        // both are checked rather than dereferenced. Answering 401 tells the
        // frontend to sign in again, which reissues a cookie in the current shape.
        if (id is null || !Enum.TryParse<UserRole>(role, out var parsedRole))
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserDto(
            id.Value,
            User.Identity?.Name ?? string.Empty,
            parsedRole));
    }
}
