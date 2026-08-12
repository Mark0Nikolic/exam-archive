using ExamArchive.Data;
using ExamArchive.Dtos;
using ExamArchive.Models;
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

    public ModerationController(ExamArchiveDbContext db)
    {
        _db = db;
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
                p.FilePath,
                p.UploadedAt,
                p.Status))
            .ToListAsync(cancellationToken);

        return Ok(papers);
    }
}
