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
    private readonly ExamArchiveDbContext _db;
    private readonly PaperFileServer _files;
    private readonly PaperSubmissionService _submissions;

    public PapersController(
        ExamArchiveDbContext db,
        PaperFileServer files,
        PaperSubmissionService submissions)
    {
        _db = db;
        _files = files;
        _submissions = submissions;
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
    [RequestSizeLimit(PaperSubmissionService.MaxTotalUploadBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadedPaperDto>> UploadPaper(
        [FromForm] UploadPaperRequest request,
        CancellationToken cancellationToken)
    {
        // Pending: anyone can reach this endpoint, so nothing submitted through it
        // is published until a moderator has looked at it.
        var result = await _submissions.SubmitAsync(
            request, PaperStatus.Pending, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }

        // 201 without a Location header: a pending paper has no address yet. It is
        // deliberately absent from GET /api/papers, and pointing at that list would
        // send the caller somewhere their paper does not appear.
        return StatusCode(StatusCodes.Status201Created, UploadedPaperDto.From(result.Paper!));
    }
}
