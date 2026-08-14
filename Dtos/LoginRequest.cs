using System.ComponentModel.DataAnnotations;

namespace ExamArchive.Dtos;

/// <summary>
/// Credentials offered at sign-in.
/// </summary>
/// <remarks>
/// The length limits here are only a guard against absurd input — a megabyte of
/// "password" should be rejected before it reaches the hasher, which would
/// otherwise dutifully spend real CPU on it. They are not a password policy, and
/// deliberately say nothing about what a valid password looks like: repeating the
/// real rules in a validation message tells an attacker the shape of the search
/// space for free.
/// </remarks>
public class LoginRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
