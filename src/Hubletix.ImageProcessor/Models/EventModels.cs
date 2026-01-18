namespace Hubletix.ImageProcessor.Models;

/// <summary>
/// EventBridge HeroImageUpdated event detail
/// </summary>
public record HeroImageUpdatedEvent
{
    /// <summary>
    /// Full canonical URL to the source image in R2
    /// Format: https://domain.com/tenant-{tenantId}/{imageId}.ext
    /// </summary>
    public required string CanonicalUrl { get; init; }

    /// <summary>
    /// Unique image key/ID from the R2 object key path
    /// Format: tenant-{tenantId}/{imageId}
    /// </summary>
    public required string ImageKey { get; init; }
}

/// <summary>
/// Image variant metadata
/// </summary>
public record ImageVariant
{
    /// <summary>
    /// Width of the resized image in pixels
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// File format/extension (avif, webp, jpeg)
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Encoding quality (50 for AVIF, 75 for WebP, 80 for JPEG)
    /// </summary>
    public required int Quality { get; init; }

    /// <summary>
    /// Version identifier (currently hardcoded to "v1")
    /// </summary>
    public string Version { get; init; } = "v1";

    /// <summary>
    /// Generated deterministic key for R2 storage
    /// Format: {baseKey}-{width}w-{format}-q{quality}-{version}.{ext}
    /// </summary>
    public required string R2Key { get; init; }

    /// <summary>
    /// Encoded image bytes
    /// </summary>
    public required byte[] ImageData { get; init; }

    /// <summary>
    /// MIME type for Content-Type header
    /// </summary>
    public string ContentType => Format switch
    {
        "avif" => "image/avif",
        "webp" => "image/webp",
        "jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}

/// <summary>
/// Image processing result
/// </summary>
public record ImageProcessingResult
{
    public required string ImageId { get; init; }
    public required string TenantId { get; init; }
    public required List<ImageVariant> SuccessfulVariants { get; init; }
    public required Dictionary<string, string> FailedVariants { get; init; }
    public bool AnyVariantsProcessed => SuccessfulVariants.Any();
}
