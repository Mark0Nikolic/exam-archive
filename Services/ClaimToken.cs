using System.Security.Cryptography;
using System.Text;

namespace ExamArchive.Services;

/// <summary>
/// The receipt code an anonymous submitter is given so they can come back and find
/// out what happened to their paper.
/// </summary>
/// <remarks>
/// This exists because uploading does not require an account, and a rejection
/// reason written for the submitter is worthless if it cannot reach them. The code
/// is the only thread connecting a person to a submission — deliberately the only
/// one, since anything more durable would mean storing who they are.
/// <para>
/// It is a bearer credential: whoever holds it can read that paper's status. That
/// is the whole security model, so it has to be unguessable rather than merely
/// unique, which rules out anything derived from the row id or the clock.
/// </para>
/// </remarks>
public static class ClaimToken
{
    /// <summary>
    /// Crockford's base32 alphabet: the digits and the uppercase letters, minus
    /// I, L, O and U.
    /// </summary>
    /// <remarks>
    /// Chosen over plain base64 because a person may well write this on the corner
    /// of a notebook and type it back a week later. I and L are dropped because
    /// they are confusable with 1, O with 0, and U so that no accidental English
    /// obscenity appears in a code shown to students.
    /// </remarks>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Characters in the generated code, before grouping dashes are added.
    /// </summary>
    /// <remarks>
    /// Twenty symbols from a 32-letter alphabet is 100 bits. Far past the point
    /// where guessing is worth attempting, and the reason no rate limit is required
    /// on the lookup for correctness — at a million tries a second, the expected
    /// search takes longer than the university has existed.
    /// </remarks>
    private const int Length = 20;

    /// <summary>Symbols per dash-separated group, for legibility only.</summary>
    private const int GroupSize = 5;

    /// <summary>
    /// Produces a fresh code in its display form, for example
    /// <c>4K7M2-QRT9X-BD3HF-WY6NP</c>.
    /// </summary>
    /// <remarks>
    /// Each byte is reduced modulo 32, which is unbiased here only because 256
    /// divides evenly by 32 — every symbol is the image of exactly eight byte
    /// values. The same trick with an alphabet of, say, 30 characters would quietly
    /// favour the first few symbols and cost entropy.
    /// </remarks>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(Length);
        var token = new StringBuilder(Length + (Length / GroupSize) - 1);

        for (var i = 0; i < Length; i++)
        {
            if (i > 0 && i % GroupSize == 0)
            {
                token.Append('-');
            }

            token.Append(Alphabet[bytes[i] % Alphabet.Length]);
        }

        return token.ToString();
    }

    /// <summary>
    /// Reduces a code as typed to the canonical form that gets hashed.
    /// </summary>
    /// <remarks>
    /// Everything a human plausibly gets wrong while copying is forgiven here:
    /// lowercase, missing or extra dashes, stray spaces, and the confusable letters
    /// the alphabet excludes. Forgiving them at lookup rather than at generation is
    /// what makes the exclusions useful — a code containing no O is only helpful if
    /// typing O still finds it.
    /// </remarks>
    public static string Normalize(string token)
    {
        var normalized = new StringBuilder(token.Length);

        foreach (var c in token)
        {
            var upper = char.ToUpperInvariant(c);

            if (upper is 'I' or 'L')
            {
                normalized.Append('1');
            }
            else if (upper is 'O')
            {
                normalized.Append('0');
            }
            else if (Alphabet.Contains(upper))
            {
                normalized.Append(upper);
            }

            // Dashes, spaces and anything else are dropped rather than rejected. A
            // lookup that fails is indistinguishable from a wrong code anyway, so
            // there is nothing to gain by being strict about punctuation.
        }

        return normalized.ToString();
    }

    /// <summary>
    /// The form stored in the database: SHA-256 of the normalized code, as hex.
    /// </summary>
    /// <remarks>
    /// A plain hash, not one of the slow password hashes. Those exist to make
    /// guessing a human-chosen secret expensive; this secret is 100 random bits, so
    /// there is no dictionary to defend against and the cost would buy nothing.
    /// <para>
    /// Hashing at all is a modest win, honestly: anyone who can read this column can
    /// already read the status and the rejection reason it protects. It is done
    /// because the token is a credential, credentials are not stored in the clear as
    /// a matter of habit, and here that habit costs one line.
    /// </para>
    /// </remarks>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(token))));
}
