namespace ExamArchive.Models;

/// <summary>
/// A file format the archive accepts, together with the byte signature that
/// proves an upload really is that format.
/// </summary>
/// <remarks>
/// A file extension is a claim made by whoever is uploading, so it is checked
/// against the bytes on disk before anything is stored. The signature is
/// expressed as segments because not every format puts its marker at offset
/// zero — WebP writes "RIFF", four bytes of length, then "WEBP".
/// </remarks>
public sealed class PaperFileType
{
    /// <summary>MIME type, stored on the row and sent back on download.</summary>
    public required string ContentType { get; init; }

    /// <summary>Canonical extension used for the file written to disk.</summary>
    public required string Extension { get; init; }

    /// <summary>Extensions a client may submit, all folded to <see cref="Extension"/>.</summary>
    public required string[] AcceptedExtensions { get; init; }

    /// <summary>Byte runs that must appear at the given offsets for a file to match.</summary>
    public required (int Offset, byte[] Bytes)[] Signature { get; init; }

    /// <summary>Longest offset+length in the signature — how many bytes must be read to test it.</summary>
    public int SignatureLength => Signature.Max(s => s.Offset + s.Bytes.Length);

    /// <summary>True when <paramref name="header"/> carries every segment of this signature.</summary>
    public bool Matches(ReadOnlySpan<byte> header)
    {
        foreach (var (offset, bytes) in Signature)
        {
            if (header.Length < offset + bytes.Length)
            {
                return false;
            }

            if (!header.Slice(offset, bytes.Length).SequenceEqual(bytes))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The formats the archive accepts: a scan produces a PDF, a phone produces an
/// image, and both are legitimate ways to submit a paper.
/// </summary>
/// <remarks>
/// This list is the single source of truth. It backs the upload validation, the
/// content type sent on download, and the CK_PaperFile_ContentType check
/// constraint — adding a format means updating that constraint in a migration
/// too, or inserts will start failing.
/// </remarks>
public static class PaperFileTypes
{
    public static readonly PaperFileType Pdf = new()
    {
        ContentType = "application/pdf",
        Extension = ".pdf",
        AcceptedExtensions = [".pdf"],
        Signature = [(0, "%PDF-"u8.ToArray())]
    };

    public static readonly PaperFileType Jpeg = new()
    {
        ContentType = "image/jpeg",
        Extension = ".jpg",
        AcceptedExtensions = [".jpg", ".jpeg"],

        // Every JPEG opens with a Start of Image marker followed by the first
        // segment's own marker byte, which varies — so only three bytes are fixed.
        Signature = [(0, [0xFF, 0xD8, 0xFF])]
    };

    public static readonly PaperFileType Png = new()
    {
        ContentType = "image/png",
        Extension = ".png",
        AcceptedExtensions = [".png"],

        // The trailing CR LF EOF bytes are deliberate: they let a PNG detect
        // having been mangled by a transfer that rewrote line endings.
        Signature = [(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])]
    };

    public static readonly PaperFileType Webp = new()
    {
        ContentType = "image/webp",
        Extension = ".webp",
        AcceptedExtensions = [".webp"],

        // Bytes 4-7 are the file length, which is why the marker is split.
        Signature = [(0, "RIFF"u8.ToArray()), (8, "WEBP"u8.ToArray())]
    };

    /// <summary>Every accepted format.</summary>
    public static readonly PaperFileType[] All = [Pdf, Jpeg, Png, Webp];

    /// <summary>Longest signature across all formats — the read size that can test any of them.</summary>
    public static readonly int MaxSignatureLength = All.Max(t => t.SignatureLength);

    /// <summary>Extensions shown to the client when an upload is rejected.</summary>
    public static readonly string[] AcceptedExtensions =
        [.. All.SelectMany(t => t.AcceptedExtensions).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Finds the format claimed by a file name's extension. The claim still has
    /// to be confirmed against the bytes — see <see cref="PaperFileType.Matches"/>.
    /// </summary>
    public static PaperFileType? FromExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        return All.FirstOrDefault(
            t => t.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Looks up a format by the content type stored on a row.</summary>
    public static PaperFileType? FromContentType(string contentType) =>
        All.FirstOrDefault(
            t => t.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
}
