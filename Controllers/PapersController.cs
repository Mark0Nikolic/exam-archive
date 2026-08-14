using ExamArchive.Data;
using ExamArchive.Dtos;
using ExamArchive.Models;
using ExamArchive.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PapersController : ControllerBase
{
    /// <summary>Per-page cap. A phone photo is a few megabytes; a scanned PDF rarely more.</summary>
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Cap across the whole submission, so thirty pages at the per-page maximum
    /// cannot be used to push 600 MB through one request.
    /// </summary>
    private const long MaxTotalUploadBytes = 100 * 1024 * 1024;

    /// <summary>Nothing in this archive predates the university's digital records.</summary>
    private const int MinYear = 1990;

    private readonly ExamArchiveDbContext _db;
    private readonly PaperFileStorage _storage;
    private readonly PaperFileServer _files;
    private readonly ILogger<PapersController> _logger;

    public PapersController(
        ExamArchiveDbContext db,
        PaperFileStorage storage,
        PaperFileServer files,
        ILogger<PapersController> logger)
    {
        _db = db;
        _storage = storage;
        _files = files;
        _logger = logger;
    }

    /// <summary>
    /// Lists the approved exam papers archived under a subject, newest exam first.
    /// </summary>
    /// <param name="subjectId">The subject whose archive to list. Required.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PaperDto>>> GetPapers(
        [FromQuery, BindRequired] int subjectId,
        CancellationToken cancellationToken)
    {
        // Filtering on SubjectId then ordering by Year/Month matches the
        // IX_Papers_SubjectId_Year_Month index exactly.
        var papers = await _db.Papers
            .AsNoTracking()
            .Where(p => p.SubjectId == subjectId && p.Status == PaperStatus.Approved)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Select(p => new PaperDto(
                p.Id,
                p.ExamType,
                p.Month,
                p.Year,
                p.Files.Count,
                p.UploadedAt))
            .ToListAsync(cancellationToken);

        return Ok(papers);
    }

    /// <summary>
    /// Lists the pages of an approved paper, in reading order.
    /// </summary>
    /// <remarks>
    /// A client needs this before fetching anything: it says how many pages there
    /// are and what each one is, so an image viewer and a PDF viewer can be chosen
    /// per page rather than guessed at.
    /// </remarks>
    [HttpGet("{id:int}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PaperFileDto>>> GetPaperFiles(
        int id,
        CancellationToken cancellationToken)
    {
        var files = await _files.ListAsync(id, approvedOnly: true, cancellationToken);

        return files is null ? NotFound() : Ok(files);
    }

    /// <summary>
    /// Serves one page of an approved paper.
    /// </summary>
    /// <param name="id">The paper.</param>
    /// <param name="pageNumber">Which page, starting at 1.</param>
    /// <param name="download">
    /// True to force a save dialog. Left false the file opens in the browser,
    /// which is what a student reading an archived paper wants.
    /// </param>
    [HttpGet("{id:int}/files/{pageNumber:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaperFile(
        int id,
        int pageNumber,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        // Same 404 whether the paper does not exist or is merely unapproved: a
        // distinct response would let anyone probe ids to discover that a pending
        // paper exists, which is exactly what keeping it off the browse API prevents.
        return await _files.ServeAsync(
            Response,
            id,
            pageNumber,
            approvedOnly: true,
            asAttachment: download,
            cancellationToken);
    }

    /// <summary>
    /// Submits an exam paper to the archive. Files are stored on disk and a paper
    /// row is created with status "Pending" — it stays out of the browse API until
    /// a moderator approves it.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxTotalUploadBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadedPaperDto>> UploadPaper(
        [FromForm] UploadPaperRequest request,
        CancellationToken cancellationToken)
    {
        // The subject name is needed for the file names anyway, so this doubles as
        // the existence check — one query instead of two.
        var subjectName = await _db.Subjects
            .AsNoTracking()
            .Where(s => s.Id == request.SubjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (subjectName is null)
        {
            ModelState.AddModelError(
                nameof(request.SubjectId),
                $"Subject {request.SubjectId} does not exist.");
        }

        // ExamType needs no checking here: model binding has already rejected
        // anything outside the enum with a 400.

        // Next year is allowed: an exam sat in January is often archived against
        // the academic year it belongs to rather than the calendar year it fell in.
        var maxYear = DateTime.UtcNow.Year + 1;
        if (request.Year < MinYear || request.Year > maxYear)
        {
            ModelState.AddModelError(
                nameof(request.Year),
                $"Year must be between {MinYear} and {maxYear}.");
        }

        // Count is enforced by attributes, so an empty list has already failed
        // model validation — skip the content checks rather than report a second,
        // confusing error on top of it.
        var fileTypes = request.Files.Count > 0
            ? await ValidateFilesAsync(request.Files, cancellationToken)
            : null;

        if (!ModelState.IsValid || fileTypes is null)
        {
            return ValidationProblem(ModelState);
        }

        var submissionId = PaperFileStorage.NewSubmissionId();
        var written = new List<string>(request.Files.Count);

        var paper = new Paper
        {
            SubjectId = request.SubjectId,
            ExamType = request.ExamType,
            Month = request.Month,
            Year = request.Year,
            Status = PaperStatus.Pending
        };

        try
        {
            // Files first, row second. The reverse order would leave a row pointing
            // at files that were never written; this order can at worst leave
            // unreferenced files, which the cleanup below handles and which are
            // harmless if that too fails.
            for (var i = 0; i < request.Files.Count; i++)
            {
                var pageNumber = i + 1;
                var type = fileTypes[i];

                var relativePath = PaperFileStorage.BuildRelativePath(
                    subjectName!, request.ExamType, request.Month, request.Year,
                    submissionId, pageNumber, type.Extension);

                var size = await _storage.SaveAsync(request.Files[i], relativePath, cancellationToken);
                written.Add(relativePath);

                paper.Files.Add(new PaperFile
                {
                    StoredPath = relativePath,
                    ContentType = type.ContentType,
                    PageNumber = pageNumber,
                    SizeBytes = size
                });
            }

            _db.Papers.Add(paper);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _storage.TryDeleteOrphans(written, _logger);
            throw;
        }

        // 201 without a Location header: a pending paper has no address yet. It is
        // deliberately absent from GET /api/papers, and pointing at that list would
        // send the caller somewhere their paper does not appear.
        return StatusCode(
            StatusCodes.Status201Created,
            new UploadedPaperDto(
                paper.Id,
                paper.SubjectId,
                paper.ExamType,
                paper.Month,
                paper.Year,
                paper.UploadedAt,
                paper.Status,
                [.. paper.Files.Select(f => new PaperFileDto(f.PageNumber, f.ContentType, f.SizeBytes))]));
    }

    /// <summary>
    /// Checks every uploaded page is a non-empty, reasonably sized file in an
    /// accepted format, recording any problems on <see cref="ModelState"/>.
    /// </summary>
    /// <returns>
    /// The resolved format of each file, positionally, or null if any file failed.
    /// </returns>
    private async Task<PaperFileType[]?> ValidateFilesAsync(
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var resolved = new PaperFileType[files.Count];
        var totalBytes = 0L;
        var valid = true;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];

            // Page numbers, not indexes: the error is read by whoever picked the
            // files, and they counted from one.
            var label = $"{nameof(UploadPaperRequest.Files)}[{i}]";
            var page = i + 1;

            totalBytes += file.Length;

            if (file.Length == 0)
            {
                ModelState.AddModelError(label, $"Page {page} is empty.");
                valid = false;
                continue;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError(
                    label,
                    $"Page {page} exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB per-file limit.");
                valid = false;
                continue;
            }

            var type = PaperFileTypes.FromExtension(file.FileName);
            if (type is null)
            {
                ModelState.AddModelError(
                    label,
                    $"Page {page} must be one of: {string.Join(", ", PaperFileTypes.AcceptedExtensions)}.");
                valid = false;
                continue;
            }

            // An extension is just a claim by the client, so confirm the header too.
            await using var stream = file.OpenReadStream();
            var header = new byte[PaperFileTypes.MaxSignatureLength];
            var read = await stream.ReadAtLeastAsync(
                header, header.Length, throwOnEndOfStream: false, cancellationToken);

            if (!type.Matches(header.AsSpan(0, read)))
            {
                ModelState.AddModelError(
                    label,
                    $"Page {page} is not a valid {type.Extension[1..].ToUpperInvariant()} file.");
                valid = false;
                continue;
            }

            resolved[i] = type;
        }

        if (totalBytes > MaxTotalUploadBytes)
        {
            ModelState.AddModelError(
                nameof(UploadPaperRequest.Files),
                $"The submission exceeds the {MaxTotalUploadBytes / (1024 * 1024)} MB total limit.");
            valid = false;
        }

        // A paper of mixed formats is almost certainly a mistake — a PDF is already
        // the whole document, so pairing it with loose images means the submitter
        // picked the wrong files.
        if (valid && resolved.Any(t => t == PaperFileTypes.Pdf) && resolved.Length > 1)
        {
            ModelState.AddModelError(
                nameof(UploadPaperRequest.Files),
                "A PDF must be submitted on its own. Upload either one PDF or a set of images.");
            valid = false;
        }

        return valid ? resolved : null;
    }
}
