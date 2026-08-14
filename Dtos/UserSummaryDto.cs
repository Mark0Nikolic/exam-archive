using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// A staff account as an administrator sees it.
/// </summary>
/// <remarks>
/// No password hash, and no field that could be mistaken for one. There is never a
/// reason to send a hash to a client — it cannot be displayed, cannot be verified
/// there, and only creates a copy of a credential in somewhere it does not belong.
/// </remarks>
/// <param name="MustChangePassword">
/// True while the account is on an administrator-issued password. Worth showing,
/// because an account sitting here for weeks means the password was handed over and
/// never picked up.
/// </param>
public record UserSummaryDto(
    int Id,
    string Username,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt);
