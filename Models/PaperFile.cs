namespace ExamArchive.Models;

/// <summary>
/// One stored file belonging to a <see cref="Paper"/> — a whole scanned PDF, or
/// a single photographed page.
/// </summary>
/// <remarks>
/// This exists because an exam is rarely one sheet. Someone photographing a
/// four-page paper produces four images that are one submission, reviewed and
/// approved as a unit, so the moderation state stays on <see cref="Paper"/> and
/// only the bytes live here.
/// </remarks>
public class PaperFile
{
    public int Id { get; set; }

    public int PaperId { get; set; }

    public Paper? Paper { get; set; }

    /// <summary>Path as stored, e.g. <c>/uploads/2024/algorithms-final-2024-06-a1b2c3d4-1.jpg</c>.</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>MIME type of the stored bytes — one of <see cref="PaperFileTypes"/>.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Position within the paper, starting at 1. Set by the upload from the order
    /// the files arrived in, since that is the order the pages were photographed.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>Size on disk. Recorded so listings can show it without touching the filesystem.</summary>
    public long SizeBytes { get; set; }
}
