using System.ComponentModel.DataAnnotations;

namespace ExamArchive.Dtos;

/// <summary>
/// The moderator's decision to turn a paper down.
/// </summary>
public class RejectPaperRequest
{
    /// <summary>
    /// Why the paper was rejected. Shown to whoever submitted it.
    /// </summary>
    /// <remarks>
    /// Required, because a rejection with no reason tells the submitter nothing
    /// and invites them to upload the same thing again. The usual causes —
    /// unreadable photo, wrong subject, pages missing — are all things they can
    /// fix if told.
    /// </remarks>
    [Required(ErrorMessage = "A reason is required so the submitter knows what to fix.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "The reason must be between 3 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
