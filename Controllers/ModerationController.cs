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
    private readonly PaperSubmissionService _submissions;

    public ModerationController(
        ExamArchiveDbContext db,
        PaperFileServer files,
        PaperSubmissionService submissions)
    {
        _db = db;
        _files = files;
        _submissions = submissions;
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
                p.Status,
                p.ReviewedAt,
                p.RejectionReason))
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

    /// <summary>
    /// Publishes a paper: it becomes visible to everyone through the browse API.
    /// </summary>
    /// <remarks>
    /// Also reverses a rejection, which clears the stored reason — leaving it
    /// behind would put a rejection note on a published paper. Approving a paper
    /// that is already approved changes nothing and still returns 200, so a
    /// double-clicked button is not an error.
    /// </remarks>
    [HttpPost("papers/{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationPaperDto>> ApprovePaper(
        int id,
        CancellationToken cancellationToken)
    {
        var paper = await _db.Papers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (paper is null)
        {
            return NotFound();
        }

        if (paper.Status != PaperStatus.Approved)
        {
            paper.Status = PaperStatus.Approved;
            paper.ReviewedAt = DateTime.UtcNow;
            paper.RejectionReason = null;

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(await LoadForModerationAsync(id, cancellationToken));
    }

    /// <summary>
    /// Turns a paper down, recording why.
    /// </summary>
    /// <remarks>
    /// The files stay on disk. A rejection is a judgement that can be revisited —
    /// a moderator misreads a page, or the submitter explains what it is — and
    /// deleting on rejection would make that unrecoverable. Papers are small; the
    /// safe direction is to keep the bytes and sweep them later if it ever matters.
    /// <para>
    /// Rejecting an already-rejected paper updates the reason, which is what a
    /// moderator correcting their own wording expects.
    /// </para>
    /// </remarks>
    [HttpPost("papers/{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationPaperDto>> RejectPaper(
        int id,
        [FromBody] RejectPaperRequest request,
        CancellationToken cancellationToken)
    {
        var paper = await _db.Papers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (paper is null)
        {
            return NotFound();
        }

        paper.Status = PaperStatus.Rejected;
        paper.ReviewedAt = DateTime.UtcNow;
        paper.RejectionReason = request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await LoadForModerationAsync(id, cancellationToken));
    }

    /// <summary>
    /// Uploads a paper on behalf of staff, published immediately.
    /// </summary>
    /// <remarks>
    /// Identical to the public upload except that the paper skips the queue: a
    /// professor or assistant adding a paper is the same authority that would have
    /// approved it, so making them upload it and then approve their own submission
    /// is a step that decides nothing.
    /// <para>
    /// SECURITY: this endpoint publishes to the archive without review, which makes
    /// it the most valuable thing here to an attacker. It lives on this controller
    /// precisely so that the eventual [Authorize] covers it — until then it is as
    /// open as the rest, and this must not be reachable from outside a development
    /// machine.
    /// </para>
    /// </remarks>
    [HttpPost("papers/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PaperSubmissionService.MaxTotalUploadBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadedPaperDto>> UploadPaper(
        [FromForm] UploadPaperRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _submissions.SubmitAsync(
            request, PaperStatus.Approved, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }

        // Unlike the public upload this one has somewhere to point: the paper is
        // approved, so it is already reachable through the browse API.
        return CreatedAtAction(
            actionName: nameof(PapersController.GetPaperFiles),
            controllerName: "Papers",
            routeValues: new { id = result.Paper!.Id },
            value: UploadedPaperDto.From(result.Paper));
    }

    /// <summary>
    /// Re-reads a paper in the shape the queue uses, so a decision returns the
    /// same row the caller was already displaying.
    /// </summary>
    private async Task<ModerationPaperDto?> LoadForModerationAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Papers
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ModerationPaperDto(
                p.Id,
                p.SubjectId,
                p.Subject!.Name,
                p.ExamType,
                p.Month,
                p.Year,
                p.Files.Count,
                p.UploadedAt,
                p.Status,
                p.ReviewedAt,
                p.RejectionReason))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
