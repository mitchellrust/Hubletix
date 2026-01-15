using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Implementation of IStorageService using Cloudflare R2 (S3-compatible API)
/// </summary>
public class CloudflareR2StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;
    private readonly ILogger<CloudflareR2StorageService> _logger;

    // Validation constants
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxDimensionPixels = 4096;
    
    // Supported MIME types
    private static readonly HashSet<string> SupportedMimeTypes = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    // Magic bytes for file type validation
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        { "image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
        { "image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { "image/webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } } // RIFF header
    };

    public CloudflareR2StorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<CloudflareR2StorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        
        _bucketName = configuration["CloudflareR2:BucketName"] 
            ?? throw new InvalidOperationException("CloudflareR2:BucketName configuration is missing");
        _publicUrl = configuration["CloudflareR2:PublicUrl"] 
            ?? throw new InvalidOperationException("CloudflareR2:PublicUrl configuration is missing");
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, string tenantId)
    {
        try
        {
            // Validate MIME type
            if (!SupportedMimeTypes.Contains(contentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException(
                    $"Unsupported file type: {contentType}. Supported types: JPEG, PNG, WebP");
            }

            // Read stream into memory for validation
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Validate file size
            if (fileBytes.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB");
            }

            // Validate magic bytes
            if (!ValidateMagicBytes(fileBytes, contentType))
            {
                throw new InvalidOperationException(
                    "File content does not match the declared MIME type");
            }

            // Validate image dimensions using ImageSharp
            using var image = Image.Load(fileBytes);
            if (image.Width > MaxDimensionPixels || image.Height > MaxDimensionPixels)
            {
                throw new InvalidOperationException(
                    $"Image dimensions exceed maximum allowed size of {MaxDimensionPixels}x{MaxDimensionPixels} pixels");
            }

            // Generate unique key: tenant-{tenantId}/{guid}.{ext}
            var extension = GetExtensionFromMimeType(contentType);
            var guid = Guid.NewGuid().ToString("N");
            var key = $"tenant-{tenantId}/{guid}{extension}";

            // Upload to R2
            // https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/#upload-and-retrieve-objects
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = new MemoryStream(fileBytes),
                ContentType = contentType,
                UseChunkEncoding = false // Disable chunked encoding for R2 compatibility
            };

            putRequest.DisablePayloadSigning = true; // Disable payload signing for R2
            putRequest.DisableDefaultChecksumValidation = true; // Disable checksum validation
            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Failed to upload image to R2. Status: {response.HttpStatusCode}");
            }

            var imageUrl = $"{_publicUrl.TrimEnd('/')}/{key}";
            _logger.LogInformation("Successfully uploaded image to R2: {ImageUrl}", imageUrl);

            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to R2 for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        try
        {
            var key = ExtractKeyFromUrl(imageUrl);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || 
                response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully deleted image from R2: {Key}", key);
            }
            else
            {
                _logger.LogWarning("Unexpected response when deleting image from R2. Key: {Key}, Status: {Status}", 
                    key, response.HttpStatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image from R2: {ImageUrl}", imageUrl);
            // Don't throw - graceful degradation for delete operations
        }
    }

    public string ExtractKeyFromUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            throw new ArgumentException("Image URL cannot be null or empty", nameof(imageUrl));
        }

        // Extract the key from the URL
        // Format: https://pub-xxxxx.r2.dev/tenant-{tenantId}/{guid}.ext
        var uri = new Uri(imageUrl);
        return uri.AbsolutePath.TrimStart('/');
    }

    private bool ValidateMagicBytes(byte[] fileBytes, string contentType)
    {
        if (!MagicBytes.TryGetValue(contentType.ToLowerInvariant(), out var signatures))
        {
            return false;
        }

        foreach (var signature in signatures)
        {
            if (fileBytes.Length >= signature.Length)
            {
                var match = true;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (fileBytes[i] != signature[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string GetExtensionFromMimeType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }
}
