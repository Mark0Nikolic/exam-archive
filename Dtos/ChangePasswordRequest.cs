using System.ComponentModel.DataAnnotations;
using ExamArchive.Services;

namespace ExamArchive.Dtos;

/// <summary>
/// A signed-in account replacing its own password.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// The password in use now, as proof that this is the account's owner rather
    /// than someone who found the machine unlocked.
    /// </summary>
    /// <remarks>
    /// No minimum beyond non-empty, deliberately. This field is checked against
    /// what is already stored, and an account whose password predates the current
    /// rule must still be able to present it in order to escape it.
    /// </remarks>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>The replacement.</summary>
    /// <remarks>
    /// The minimum is <see cref="UserAccountService.MinimumPasswordLength"/> rather
    /// than a number written here, and the message reads it back through
    /// StringLength's own {2} placeholder, so raising the policy in one place
    /// updates both the rule and what the user is told about it.
    /// </remarks>
    [Required]
    [StringLength(
        128,
        MinimumLength = UserAccountService.MinimumPasswordLength,
        ErrorMessage = "The new password must be at least {2} characters.")]
    public string NewPassword { get; set; } = string.Empty;
}
