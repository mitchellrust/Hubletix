using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Hubletix.ImageProcessor.Services;

/// <summary>
/// Handles Cloudflare R2 (S3-compatible) storage operations
/// </summary>
public interface IR2StorageService
{
    /// <summary>
    /// Download image from R2 using canonical URL
    /// </summary>
    Task<Stream> DownloadImageAsync(string canonicalUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a variant already exists in R2
    /// </summary>
    Task<bool> VariantExistsAsync(string r2Key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload encoded variant to R2 with appropriate metadata
    /// </summary>
    Task UploadVariantAsync(
        string r2Key,
        byte[] imageData,
        string contentType,
        string sourceImageKey,
        CancellationToken cancellationToken = default);
}

public class R2StorageService : IR2StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<R2StorageService> _logger;

    public R2StorageService(
        IAmazonS3 s3Client,
        string bucketName,
        ILogger<R2StorageService> logger)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
        _logger = logger;
    }

    public async Task<Stream> DownloadImageAsync(string canonicalUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Downloading image from R2: {CanonicalUrl}", canonicalUrl);

        // Parse the R2 key from the URL
        // Format: https://domain.com/tenant-{tenantId}/{imageId}.ext
        var uri = new Uri(canonicalUrl);
        var key = uri.AbsolutePath.TrimStart('/');

        try
        {
            var response = await _s3Client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                },
                cancellationToken);

            _logger.LogInformation("Successfully downloaded image from R2: {Key}", key);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from R2: {Key}", key);
            throw;
        }
    }

    public async Task<bool> VariantExistsAsync(string r2Key, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking if variant exists: {R2Key}", r2Key);
            await _s3Client.GetObjectMetadataAsync(
                _bucketName,
                r2Key,
                cancellationToken);

            _logger.LogInformation("Variant already exists: {R2Key}", r2Key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Variant does not exist: {R2Key}", r2Key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking variant existence: {R2Key}", r2Key);
            throw;
        }
    }

    public async Task UploadVariantAsync(
        string r2Key,
        byte[] imageData,
        string contentType,
        string sourceImageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Uploading variant to R2: {R2Key}", r2Key);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = r2Key,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead // Make publicly readable
            };

            // Set immutable cache control headers
            request.Headers["Cache-Control"] = "public, max-age=31536000, immutable";

            // Add metadata for tracking source image
            request.Metadata.Add("x-amz-meta-source-key", sourceImageKey);
            request.Metadata.Add("x-amz-meta-generated-at", System.DateTime.UtcNow.ToString("O"));

            using (var ms = new MemoryStream(imageData))
            {
                request.InputStream = ms;
                await _s3Client.PutObjectAsync(request, cancellationToken);
            }

            _logger.LogInformation(
                "Successfully uploaded variant to R2: {R2Key} ({ContentType}, {ByteSize} bytes)",
                r2Key,
                contentType,
                imageData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload variant to R2: {R2Key}", r2Key);
            throw;
        }
    }
}
