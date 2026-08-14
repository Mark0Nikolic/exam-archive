using ExamArchive.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ExamArchive.Services;

/// <summary>
/// Removes the metadata a camera writes into a photograph before it is stored.
/// </summary>
/// <remarks>
/// A phone photo carries an EXIF block holding GPS coordinates to roughly ten
/// centimetres, the exact capture time, the device make and model, and on many
/// cameras a serial number. A student photographing a paper at their desk
/// therefore embeds their home address in the upload, and approved papers are
/// served publicly with no authentication — so the file would hand out that
/// address to anyone who downloads it. Reading it back takes no tools at all:
/// on Windows it is right-click, Properties, Details.
/// <para>
/// Stripping happens on upload rather than on approval, so the data is gone
/// before it is ever written to disk and a moderator's machine is not the first
/// place it gets handled.
/// </para>
/// </remarks>
public sealed class ImageSanitizer
{
    /// <summary>
    /// Quality used when re-encoding JPEG.
    /// </summary>
    /// <remarks>
    /// Deliberately high. Clearing metadata means decoding and re-encoding, and a
    /// JPEG loses a little each time that happens — on a photograph of printed
    /// text, compression artefacts land on the letter edges, which is exactly
    /// what has to stay readable.
    /// </remarks>
    private const int JpegQuality = 92;

    /// <summary>
    /// Largest image accepted, in total pixels — about 80 megapixels.
    /// </summary>
    /// <remarks>
    /// Guards against a decompression bomb: a few kilobytes of highly compressed
    /// file can describe an image of enormous dimensions, and decoding it would
    /// allocate gigabytes. Dimensions are read from the header before any pixels
    /// are decoded, so an oversized image is refused rather than survived.
    /// </remarks>
    private const long MaxPixels = 80_000_000;

    private readonly ILogger<ImageSanitizer> _logger;

    public ImageSanitizer(ILogger<ImageSanitizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// True for formats this can clean. PDFs carry their own metadata in a
    /// different structure and pass through untouched.
    /// </summary>
    public static bool CanSanitize(PaperFileType type) => type != PaperFileTypes.Pdf;

    /// <summary>
    /// Returns a stream of the image with all metadata removed, or null if the
    /// image could not be read or is too large to decode safely.
    /// </summary>
    /// <remarks>
    /// The result is buffered in memory because the caller needs its length before
    /// writing, and an encoder cannot report that up front. Per-file size is capped
    /// well below anything that makes this a problem.
    /// </remarks>
    public async Task<MemoryStream?> SanitizeAsync(
        IFormFile file,
        PaperFileType type,
        CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();

        try
        {
            // Header only — this reads dimensions without decoding pixels, which is
            // what makes the size check a defence rather than a formality.
            var info = await Image.IdentifyAsync(source, cancellationToken);

            if ((long)info.Width * info.Height > MaxPixels)
            {
                _logger.LogWarning(
                    "Refused an upload of {Width}x{Height}, which exceeds the {MaxPixels} pixel limit.",
                    info.Width, info.Height, MaxPixels);

                return null;
            }

            source.Position = 0;
            using var image = await Image.LoadAsync(source, cancellationToken);

            // Order matters. Orientation is itself an EXIF tag: a portrait photo is
            // usually stored landscape with a tag saying to rotate it. Clearing
            // metadata first would leave every such photo lying on its side, so the
            // rotation is baked into the pixels before anything is discarded.
            image.Mutate(context => context.AutoOrient());

            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;

            var cleaned = new MemoryStream();
            await image.SaveAsync(cleaned, EncoderFor(type), cancellationToken);
            cleaned.Position = 0;

            return cleaned;
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            // The magic-byte check passed, so the file starts like an image but its
            // contents are damaged or malformed. Refuse it rather than store
            // something no viewer will open.
            _logger.LogWarning(ex, "Refused an upload that could not be decoded as an image.");
            return null;
        }
    }

    private static SixLabors.ImageSharp.Formats.ImageEncoder EncoderFor(PaperFileType type)
    {
        if (type == PaperFileTypes.Jpeg)
        {
            return new JpegEncoder { Quality = JpegQuality };
        }

        if (type == PaperFileTypes.Png)
        {
            return new PngEncoder();
        }

        if (type == PaperFileTypes.Webp)
        {
            return new WebpEncoder();
        }

        throw new ArgumentOutOfRangeException(
            nameof(type), type.ContentType, "No encoder is configured for this format.");
    }
}
