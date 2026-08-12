using System.Globalization;
using System.Text;
using ExamArchive.Data;
using ExamArchive.Dtos;
using ExamArchive.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PapersController : ControllerBase
{
    /// <summary>
    /// The only status this controller will ever serve. Not a parameter: pending
    /// and rejected uploads must not be reachable from the public browse API.
    /// </summary>
    private const string ApprovedStatus = "Approved";

    /// <summary>Every upload lands here. Promotion to Approved is a later, separate step.</summary>
    private const string PendingStatus = "Pending";

    /// <summary>Matches the CK_Paper_ExamType check constraint. Submitted values are folded to these.</summary>
    private static readonly string[] AllowedExamTypes = ["Midterm", "Final", "Resit"];

    /// <summary>The archive holds scanned exam papers, so PDF only.</summary>
    private const string AllowedExtension = ".pdf";

    private const string PdfContentType = "application/pdf";

    /// <summary>Leading segment of every stored path, kept out of the on-disk layout.</summary>
    private const string UploadsPrefix = "uploads/";

    /// <summary>First bytes of every PDF, checked so a renamed file cannot slip through on extension alone.</summary>
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>Nothing in this archive predates the university's digital records.</summary>
    private const int MinYear = 1990;

    private readonly ExamArchiveDbContext _db;
    private readonly ILogger<PapersController> _logger;

    /// <summary>Absolute path to the folder uploads are written under.</summary>
    private readonly string _uploadsRoot;

    public PapersController(
        ExamArchiveDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<PapersController> logger)
    {
        _db = db;
        _logger = logger;

        // Same reasoning as the connection string in Program.cs: a relative path
        // would resolve against the process working directory, so uploads would
        // scatter depending on how the app was started. Anchor it to the content root.
        var configured = configuration["Storage:UploadsRoot"] ?? "uploads";
        _uploadsRoot = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
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
            .Where(p => p.SubjectId == subjectId && p.Status == ApprovedStatus)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Select(p => new PaperDto(
                p.Id,
                p.ExamType,
                p.Month,
                p.Year,
                p.FilePath,
                p.UploadedAt))
            .ToListAsync(cancellationToken);

        return Ok(papers);
    }

    /// <summary>
    /// Downloads the PDF for an approved paper.
    /// </summary>
    /// <param name="id">The paper to download.</param>
    [HttpGet("{id:int}/download")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPaper(int id, CancellationToken cancellationToken)
    {
        var paper = await _db.Papers
            .AsNoTracking()
            .Where(p => p.Id == id && p.Status == ApprovedStatus)
            .Select(p => new
            {
                p.FilePath,
                SubjectName = p.Subject!.Name,
                p.ExamType,
                p.Month,
                p.Year
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Same 404 whether the paper does not exist or is merely unapproved: a
        // distinct response would let anyone probe ids to discover that a pending
        // paper exists, which is exactly what keeping it off the browse API prevents.
        if (paper is null)
        {
            return NotFound();
        }

        if (!TryResolveStoredPath(paper.FilePath, out var absolutePath))
        {
            // Only reachable if a row's path was written by something other than
            // the upload endpoint, so treat it as data corruption rather than a miss.
            _logger.LogError(
                "Paper {PaperId} has stored path {Path}, which escapes the uploads root.",
                id,
                paper.FilePath);

            return NotFound();
        }

        if (!System.IO.File.Exists(absolutePath))
        {
            // The row and the disk have drifted apart. Worth a log line — it means
            // a file was removed out from under the archive.
            _logger.LogWarning(
                "Paper {PaperId} points at {Path}, which is missing from disk.",
                id,
                absolutePath);

            return NotFound();
        }

        // Rebuilt from the row rather than reusing the stored name, which carries a
        // uniqueness suffix that means nothing to whoever is saving the file.
        var downloadName = string.Create(
            CultureInfo.InvariantCulture,
            $"{Slugify(paper.SubjectName)}-{paper.ExamType.ToLowerInvariant()}-{paper.Year}-{paper.Month:D2}{AllowedExtension}");

        // Range processing on: PDF viewers fetch the trailer first, then jump to the
        // pages they need, rather than pulling the whole file down to show page one.
        return PhysicalFile(absolutePath, PdfContentType, downloadName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Submits an exam paper to the archive. The file is stored on disk and a
    /// paper row is created with status "Pending" — it stays out of the browse
    /// API until a moderator approves it.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadedPaperDto>> UploadPaper(
        [FromForm] UploadPaperRequest request,
        CancellationToken cancellationToken)
    {
        // The subject name is needed for the file name anyway, so this doubles as
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

        // Folded to the canonical casing so "final" does not trip CK_Paper_ExamType.
        var examType = AllowedExamTypes.FirstOrDefault(
            t => t.Equals(request.ExamType, StringComparison.OrdinalIgnoreCase));

        if (examType is null)
        {
            ModelState.AddModelError(
                nameof(request.ExamType),
                $"ExamType must be one of: {string.Join(", ", AllowedExamTypes)}.");
        }

        // Next year is allowed: an exam sat in January is often archived against
        // the academic year it belongs to rather than the calendar year it fell in.
        var maxYear = DateTime.UtcNow.Year + 1;
        if (request.Year < MinYear || request.Year > maxYear)
        {
            ModelState.AddModelError(
                nameof(request.Year),
                $"Year must be between {MinYear} and {maxYear}.");
        }

        // File is [Required], so a null one has already failed model validation —
        // skip the content checks rather than report a second, confusing error.
        if (request.File is not null)
        {
            await ValidateFileAsync(request.File, cancellationToken);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var relativePath = BuildRelativePath(subjectName!, examType!, request.Month, request.Year);

        if (!TryResolveStoredPath(relativePath, out var absolutePath))
        {
            // BuildRelativePath only ever produces paths under the uploads root, so
            // reaching this means the generator itself is broken.
            throw new InvalidOperationException(
                $"Generated upload path '{relativePath}' resolved outside the uploads root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        // CreateNew rather than Create: if the generated name somehow already
        // exists, fail loudly instead of overwriting somebody else's paper.
        await using (var destination = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await request.File!.CopyToAsync(destination, cancellationToken);
        }

        var paper = new Paper
        {
            SubjectId = request.SubjectId,
            FilePath = relativePath,
            ExamType = examType!,
            Month = request.Month,
            Year = request.Year,
            Status = PendingStatus
        };

        try
        {
            // File first, row second. The reverse order would leave a row in the
            // browse index pointing at a file that was never written; this order
            // can at worst leave an unreferenced file, which the cleanup below
            // handles and which is harmless if that too fails.
            _db.Papers.Add(paper);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            TryDeleteOrphan(absolutePath);
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
                paper.FilePath,
                paper.UploadedAt,
                paper.Status));
    }

    /// <summary>
    /// Checks the uploaded file is a non-empty, reasonably sized PDF, recording
    /// any problems on <see cref="ModelState"/>.
    /// </summary>
    private async Task ValidateFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            ModelState.AddModelError(nameof(UploadPaperRequest.File), "The file is empty.");
            return;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError(
                nameof(UploadPaperRequest.File),
                $"The file exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.");
            return;
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(UploadPaperRequest.File),
                $"Only {AllowedExtension} files are accepted.");
            return;
        }

        // An extension is just a claim by the client, so confirm the header too.
        await using var stream = file.OpenReadStream();
        var header = new byte[PdfMagic.Length];
        var read = await stream.ReadAtLeastAsync(
            header, header.Length, throwOnEndOfStream: false, cancellationToken);

        if (read < header.Length || !header.SequenceEqual(PdfMagic))
        {
            ModelState.AddModelError(
                nameof(UploadPaperRequest.File),
                "The file is not a valid PDF.");
        }
    }

    /// <summary>
    /// Builds the stored path, matching the /uploads/{year}/{name}.pdf shape the
    /// existing rows use. The client's file name is never part of it — it is
    /// attacker-controlled and could carry separators or traversal segments.
    /// </summary>
    private static string BuildRelativePath(string subjectName, string examType, int month, int year)
    {
        // Short suffix keeps the name readable while separating repeat uploads of
        // the same exam, which are otherwise identical.
        var unique = Guid.NewGuid().ToString("N")[..8];
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{Slugify(subjectName)}-{examType.ToLowerInvariant()}-{year}-{month:D2}-{unique}{AllowedExtension}");

        return $"/uploads/{year}/{name}";
    }

    /// <summary>
    /// Maps a stored path onto the configured uploads folder, refusing anything that
    /// resolves outside it. Paths from <see cref="BuildRelativePath"/> are always
    /// safe; rows edited by hand, seeded, or restored from a backup may not be, and
    /// this is the one place a stored path becomes a filesystem read.
    /// </summary>
    private bool TryResolveStoredPath(string storedPath, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        var relative = storedPath.Replace('\\', '/').TrimStart('/');

        if (relative.StartsWith(UploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[UploadsPrefix.Length..];
        }

        var root = Path.GetFullPath(_uploadsRoot).TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        // GetFullPath has already collapsed any ".." segments, so comparing prefixes
        // is meaningful here in a way that inspecting the raw string would not be.
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    /// <summary>Lowercases to ASCII letters, digits and single dashes — safe on any filesystem.</summary>
    private static string Slugify(string value)
    {
        var slug = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-') is { Length: > 0 } trimmed ? trimmed : "subject";
    }

    /// <summary>
    /// Removes a file whose row failed to save. Best effort: the upload has already
    /// failed, and masking that with a delete error would only make it harder to diagnose.
    /// </summary>
    private void TryDeleteOrphan(string absolutePath)
    {
        try
        {
            System.IO.File.Delete(absolutePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to remove orphaned upload {Path} after the paper row could not be saved.",
                absolutePath);
        }
    }
}
