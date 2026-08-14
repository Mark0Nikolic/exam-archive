using ExamArchive.Data;
using ExamArchive.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Services;

/// <summary>
/// Turns a paper id and page number into a file response.
/// </summary>
/// <remarks>
/// Both the public browse API and the moderation queue serve stored pages, and
/// the only difference between them is whether unapproved papers are visible.
/// That difference is the security boundary between the two, so the surrounding
/// logic lives here as one implementation rather than as two that could drift:
/// a containment check silently dropped from one copy would be invisible until
/// it was exploited.
/// </remarks>
public sealed class PaperFileServer
{
    private readonly ExamArchiveDbContext _db;
    private readonly PaperFileStorage _storage;
    private readonly ILogger<PaperFileServer> _logger;

    public PaperFileServer(
        ExamArchiveDbContext db,
        PaperFileStorage storage,
        ILogger<PaperFileServer> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Serves one page, or a 404 if it is not visible to this caller, not on
    /// disk, or stored at a path that escapes the uploads folder.
    /// </summary>
    /// <param name="approvedOnly">
    /// True for the public API, where a pending paper must be indistinguishable
    /// from one that does not exist. False for moderation, which reviews exactly
    /// the papers the public cannot see.
    /// </param>
    /// <param name="asAttachment">True to force a save dialog, false to display in the browser.</param>
    public async Task<IActionResult> ServeAsync(
        HttpResponse response,
        int paperId,
        int pageNumber,
        bool approvedOnly,
        bool asAttachment,
        CancellationToken cancellationToken)
    {
        var page = await _db.PaperFiles
            .AsNoTracking()
            .Where(f => f.PaperId == paperId && f.PageNumber == pageNumber)
            .Where(f => !approvedOnly || f.Paper!.Status == PaperStatus.Approved)
            .Select(f => new
            {
                f.StoredPath,
                f.ContentType,
                SubjectName = f.Paper!.Subject!.Name,
                f.Paper.ExamType,
                f.Paper.Month,
                f.Paper.Year,
                PageCount = f.Paper.Files.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (page is null)
        {
            return new NotFoundResult();
        }

        if (!_storage.TryResolve(page.StoredPath, out var absolutePath))
        {
            // Only reachable if a row's path was written by something other than
            // the upload endpoint, so treat it as data corruption rather than a miss.
            _logger.LogError(
                "Paper {PaperId} page {PageNumber} has stored path {Path}, which escapes the uploads root.",
                paperId, pageNumber, page.StoredPath);

            return new NotFoundResult();
        }

        if (!File.Exists(absolutePath))
        {
            // The row and the disk have drifted apart. Worth a log line — it means
            // a file was removed out from under the archive.
            _logger.LogWarning(
                "Paper {PaperId} page {PageNumber} points at {Path}, which is missing from disk.",
                paperId, pageNumber, absolutePath);

            return new NotFoundResult();
        }

        var extension = PaperFileTypes.FromContentType(page.ContentType)?.Extension ?? string.Empty;

        PaperFileStorage.SetFileHeaders(
            response,
            PaperFileStorage.BuildDownloadName(
                page.SubjectName, page.ExamType, page.Month, page.Year,
                pageNumber, page.PageCount, extension),
            asAttachment);

        // Range processing on: PDF viewers fetch the trailer first, then jump to the
        // pages they need, rather than pulling the whole file down to show page one.
        return new PhysicalFileResult(absolutePath, page.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    /// <summary>
    /// Lists a paper's pages, or null if the paper is not visible to this caller.
    /// </summary>
    public async Task<List<Dtos.PaperFileDto>?> ListAsync(
        int paperId,
        bool approvedOnly,
        CancellationToken cancellationToken)
    {
        var visible = await _db.Papers
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == paperId && (!approvedOnly || p.Status == PaperStatus.Approved),
                cancellationToken);

        if (!visible)
        {
            return null;
        }

        return await _db.PaperFiles
            .AsNoTracking()
            .Where(f => f.PaperId == paperId)
            .OrderBy(f => f.PageNumber)
            .Select(f => new Dtos.PaperFileDto(f.PageNumber, f.ContentType, f.SizeBytes))
            .ToListAsync(cancellationToken);
    }
}
