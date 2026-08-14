using ExamArchive.Data;
using ExamArchive.Dtos;
using ExamArchive.Models;
using ExamArchive.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Controllers;

/// <summary>
/// Staff-facing review of submitted papers.
/// </summary>
/// <remarks>
/// Split from <see cref="PapersController"/> along the line of who may call it:
/// that controller is public, anonymous and read-only, this one is for staff and
/// will hold the write operations. Keeping them apart means securing moderation
/// is a single attribute on this class rather than an attribute on every action,
/// where one omission would leave a hole.
/// <para>
/// SECURITY: there is no authentication yet, so every endpoint here is currently
/// open to anyone who knows the URL. See the note on <see cref="GetPapers"/>.
/// </para>
/// </remarks>
[ApiController]
[Route("api/moderation")]
[Produces("application/json")]
public class ModerationController : ControllerBase
{
    private readonly ExamArchiveDbContext _db;
    private readonly PaperFileServer _files;

    public ModerationController(ExamArchiveDbContext db, PaperFileServer files)
    {
        _db = db;
        _files = files;
    }

    /// <summary>
    /// Lists papers awaiting review, longest-waiting first.
    /// </summary>
    /// <param name="status">
    /// Which queue to read. Defaults to <see cref="PaperStatus.Pending"/> — the
    /// work list. Passing Approved or Rejected reviews past decisions.
    /// </param>
    /// <remarks>
    /// SECURITY: unauthenticated, so this currently exposes unapproved and
    /// rejected submissions to any caller. That is the one thing the browse API
    /// deliberately prevents, so authentication must land before this is reachable
    /// from anywhere but a development machine.
    /// </remarks>
    [HttpGet("papers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ModerationPaperDto>>> GetPapers(
        [FromQuery] PaperStatus status = PaperStatus.Pending,
        CancellationToken cancellationToken = default)
    {
        // Oldest first, the opposite of the browse API. This is a work queue, so
        // the paper that has been waiting longest is the one to deal with next;
        // newest-first would let an old submission sit at the bottom forever.
        var papers = await _db.Papers
            .AsNoTracking()
            .Where(p => p.Status == status)
            .OrderBy(p => p.UploadedAt)
            .ThenBy(p => p.Id)
            .Select(p => new ModerationPaperDto(
                p.Id,
                p.SubjectId,
                p.Subject!.Name,
                p.ExamType,
                p.Month,
                p.Year,
                p.Files.Count,
                p.UploadedAt,
                p.Status))
            .ToListAsync(cancellationToken);

        return Ok(papers);
    }

    /// <summary>
    /// Lists the pages of a submitted paper, whatever its status.
    /// </summary>
    /// <remarks>
    /// The moderation counterpart to the browse listing: a reviewer needs the page
    /// count and formats before opening anything, and unlike the public API this
    /// answers for pending and rejected papers too.
    /// </remarks>
    [HttpGet("papers/{id:int}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PaperFileDto>>> GetPaperFiles(
        int id,
        CancellationToken cancellationToken)
    {
        var files = await _files.ListAsync(id, approvedOnly: false, cancellationToken);

        return files is null ? NotFound() : Ok(files);
    }

    /// <summary>
    /// Opens one page of a submitted paper for review, in the browser.
    /// </summary>
    /// <param name="id">The paper being reviewed.</param>
    /// <param name="pageNumber">Which page, starting at 1.</param>
    /// <param name="download">True to save the file instead of viewing it.</param>
    /// <remarks>
    /// Served inline so review is a matter of opening a tab rather than
    /// downloading, opening, and then deleting a file per submission.
    /// <para>
    /// SECURITY: this returns unreviewed, submitter-supplied bytes, which is the
    /// riskiest thing the application does. The response carries
    /// X-Content-Type-Options: nosniff so a browser cannot decide the file is
    /// really HTML and execute it against this origin.
    /// </para>
    /// </remarks>
    [HttpGet("papers/{id:int}/files/{pageNumber:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaperFile(
        int id,
        int pageNumber,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        return await _files.ServeAsync(
            Response,
            id,
            pageNumber,
            approvedOnly: false,
            asAttachment: download,
            cancellationToken);
    }
}
