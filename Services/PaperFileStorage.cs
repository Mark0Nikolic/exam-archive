using System.Globalization;
using System.Text;
using ExamArchive.Models;
using Microsoft.Net.Http.Headers;

namespace ExamArchive.Services;

/// <summary>
/// Owns the uploads folder: where a paper's pages are written, and how a stored
/// path is turned back into a file that may safely be read.
/// </summary>
/// <remarks>
/// Extracted from the upload controller once moderators needed to preview files
/// too. Both controllers must apply the same containment rule, and a rule
/// implemented twice is a rule that eventually differs in one place.
/// </remarks>
public sealed class PaperFileStorage
{
    /// <summary>Leading segment of every stored path, kept out of the on-disk layout.</summary>
    private const string UploadsPrefix = "uploads/";

    /// <summary>Absolute path to the folder uploads are written under.</summary>
    private readonly string _root;

    public PaperFileStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        // Same reasoning as the connection string in Program.cs: a relative path
        // would resolve against the process working directory, so uploads would
        // scatter depending on how the app was started. Anchor it to the content root.
        var configured = configuration["Storage:UploadsRoot"] ?? "uploads";
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
    }

    /// <summary>
    /// Builds the stored path for one page. The client's file name is never part
    /// of it — that name is attacker-controlled and could carry separators or
    /// traversal segments.
    /// </summary>
    /// <param name="submissionId">
    /// Short token shared by every page of one submission, so a paper's files sit
    /// together when the folder is read by eye.
    /// </param>
    public static string BuildRelativePath(
        string? subjectCode,
        string subjectName,
        ExamType examType,
        int month,
        int year,
        string submissionId,
        int pageNumber,
        string extension)
    {
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{SubjectSlug(subjectCode, subjectName)}-{examType.ToString().ToLowerInvariant()}-{year}-{month:D2}-{submissionId}-{pageNumber:D3}{extension}");

        return $"/uploads/{year}/{name}";
    }

    /// <summary>
    /// The subject part of a file name: the course code where there is one, the
    /// slugified name otherwise.
    /// </summary>
    /// <remarks>
    /// The code is preferred because it is the only name-like field that does not
    /// change with the reader's language, and because it is plain ASCII. Both
    /// matter for a path on disk: a Cyrillic filename is legal on NTFS and ext4
    /// but awkward the moment a backup script, an rsync, or a shell on a
    /// differently-configured machine touches it. The code also does not move when
    /// somebody fixes a typo in a subject name, so stored paths stay honest.
    /// </remarks>
    private static string SubjectSlug(string? subjectCode, string subjectName) =>
        string.IsNullOrWhiteSpace(subjectCode) ? Slugify(subjectName) : Slugify(subjectCode);

    /// <summary>A short token identifying one submission, shared across its pages.</summary>
    public static string NewSubmissionId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Maps a stored path onto the uploads folder, refusing anything that resolves
    /// outside it. Paths from <see cref="BuildRelativePath"/> are always safe; rows
    /// edited by hand, seeded, or restored from a backup may not be, and this is
    /// the one place a stored path becomes a filesystem read.
    /// </summary>
    public bool TryResolve(string storedPath, out string absolutePath)
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

        var root = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar);
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

    /// <summary>
    /// Writes one page to <paramref name="relativePath"/> and returns the bytes
    /// written.
    /// </summary>
    /// <param name="content">
    /// The bytes to store — for images this is the sanitized stream rather than
    /// the uploaded one, so stripped metadata never reaches the disk.
    /// </param>
    public async Task<long> SaveAsync(
        Stream content,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!TryResolve(relativePath, out var absolutePath))
        {
            // BuildRelativePath only ever produces paths under the uploads root, so
            // reaching this means the generator itself is broken.
            throw new InvalidOperationException(
                $"Generated upload path '{relativePath}' resolved outside the uploads root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        // CreateNew rather than Create: if the generated name somehow already
        // exists, fail loudly instead of overwriting somebody else's paper.
        await using var destination = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        await content.CopyToAsync(destination, cancellationToken);

        return destination.Length;
    }

    /// <summary>
    /// Removes files left behind by an upload that failed partway. Best effort:
    /// the upload has already failed, and masking that with a delete error would
    /// only make it harder to diagnose.
    /// </summary>
    public void TryDeleteOrphans(IEnumerable<string> relativePaths, ILogger logger)
    {
        foreach (var relativePath in relativePaths)
        {
            if (!TryResolve(relativePath, out var absolutePath))
            {
                continue;
            }

            try
            {
                File.Delete(absolutePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to remove orphaned upload {Path} after the paper could not be saved.",
                    absolutePath);
            }
        }
    }

    /// <summary>
    /// Builds the name a download is offered under. Rebuilt from the row rather
    /// than reusing the stored name, which carries a uniqueness suffix that means
    /// nothing to whoever is saving the file.
    /// </summary>
    /// <remarks>
    /// Uses the course code for the same reason the stored path does: with three
    /// display languages there is no single "the name", and picking one would hand
    /// an English-reading student a Cyrillic file name or the reverse. IT240 reads
    /// the same to everyone, and it is what students call the course anyway.
    /// </remarks>
    public static string BuildDownloadName(
        string? subjectCode,
        string subjectName,
        ExamType examType,
        int month,
        int year,
        int pageNumber,
        int pageCount,
        string extension)
    {
        var stem = string.Create(
            CultureInfo.InvariantCulture,
            $"{SubjectSlug(subjectCode, subjectName)}-{examType.ToString().ToLowerInvariant()}-{year}-{month:D2}");

        // Single-page papers keep the clean name they had before multi-page
        // uploads existed; only genuine multi-page papers carry a page suffix.
        return pageCount > 1
            ? string.Create(CultureInfo.InvariantCulture, $"{stem}-p{pageNumber:D2}{extension}")
            : $"{stem}{extension}";
    }

    /// <summary>
    /// Sets the response headers for serving a stored file.
    /// </summary>
    /// <param name="asAttachment">
    /// True to make the browser save the file, false to let it display in a tab.
    /// Inline is what makes review practical — a moderator checking a queue should
    /// not have to download and open each submission.
    /// </param>
    /// <remarks>
    /// nosniff is not optional here. These bytes were supplied by an unauthenticated
    /// submitter and are served inline, so without it a browser is free to ignore the
    /// declared content type, decide the file looks like HTML, and run it as script
    /// from this origin.
    /// </remarks>
    public static void SetFileHeaders(HttpResponse response, string downloadName, bool asAttachment)
    {
        response.Headers.XContentTypeOptions = "nosniff";

        var disposition = new ContentDispositionHeaderValue(asAttachment ? "attachment" : "inline");

        // Handles the quoting and the RFC 5987 encoded form for non-ASCII names,
        // which a hand-built header string reliably gets wrong.
        disposition.SetHttpFileName(downloadName);

        response.Headers.ContentDisposition = disposition.ToString();
    }

    /// <summary>
    /// Cyrillic letters mapped to their Latin equivalents, so a Serbian name
    /// survives slugification instead of being deleted character by character.
    /// </summary>
    /// <remarks>
    /// This is the direction that works: њ is always "nj", whereas reading "nj"
    /// back gives one letter in "коњ" and two in "инјекција". Three letters expand
    /// to two characters, which is why this is a string map and not a char one.
    /// </remarks>
    private static readonly Dictionary<char, string> CyrillicToLatin = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['ђ'] = "dj", ['е'] = "e", ['ж'] = "z", ['з'] = "z", ['и'] = "i",
        ['ј'] = "j", ['к'] = "k", ['л'] = "l", ['љ'] = "lj", ['м'] = "m",
        ['н'] = "n", ['њ'] = "nj", ['о'] = "o", ['п'] = "p", ['р'] = "r",
        ['с'] = "s", ['т'] = "t", ['ћ'] = "c", ['у'] = "u", ['ф'] = "f",
        ['х'] = "h", ['ц'] = "c", ['ч'] = "c", ['џ'] = "dz", ['ш'] = "s"
    };

    /// <summary>
    /// Latin letters carrying Serbian diacritics, folded to plain ASCII.
    /// </summary>
    private static readonly Dictionary<char, string> LatinDiacritics = new()
    {
        ['č'] = "c", ['ć'] = "c", ['đ'] = "dj", ['š'] = "s", ['ž'] = "z"
    };

    /// <summary>
    /// Reduces a name to lowercase ASCII letters, digits and single dashes — safe
    /// on any filesystem, in any locale, through any backup tool.
    /// </summary>
    /// <remarks>
    /// Serbian text is transliterated rather than stripped. Deleting non-ASCII
    /// characters outright turned «Базе података» into an empty string and every
    /// upload for that subject into a file called "subject", and "Zaštita" into
    /// "za-tita".
    /// </remarks>
    private static string Slugify(string value)
    {
        var slug = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            var lower = char.ToLowerInvariant(c);

            if (char.IsAsciiLetterOrDigit(lower))
            {
                slug.Append(lower);
            }
            else if (CyrillicToLatin.TryGetValue(lower, out var cyrillic))
            {
                slug.Append(cyrillic);
            }
            else if (LatinDiacritics.TryGetValue(lower, out var diacritic))
            {
                slug.Append(diacritic);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-') is { Length: > 0 } trimmed ? trimmed : "subject";
    }
}
