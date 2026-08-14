using ExamArchive.Models;

namespace ExamArchive.Dtos;

/// <summary>
/// A newly created or newly reset account, together with the password to hand over.
/// </summary>
/// <remarks>
/// The only response in the application that carries a usable password, and it
/// appears exactly once — the server stores a hash and cannot produce it again. If
/// the admin closes the window without reading it out, the fix is another reset, not
/// a lookup.
/// <para>
/// A frontend should treat this like the claim code: shown large, not logged, and
/// not left on screen afterwards.
/// </para>
/// </remarks>
public record UserCredentialDto(
    int Id,
    string Username,
    UserRole Role,
    string TemporaryPassword);
