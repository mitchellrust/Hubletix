using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Hubletix.ImageProcessor.Models;
using Hubletix.ImageProcessor.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Hubletix.ImageProcessor.Handlers;

/// <summary>
/// AWS Lambda handler for EventBridge HeroImageUpdated events
/// Processes hero images and generates optimized variants
/// </summary>
public class HeroImageEventHandler
{
    private readonly IHeroImageProcessingService _processingService;
    private readonly ILogger<HeroImageEventHandler> _logger;

    // EventBridge event structure
    public record EventBridgeEvent
    {
        [JsonPropertyName("detail")]
        public required HeroImageUpdatedEvent Detail { get; init; }

        [JsonPropertyName("detail-type")]
        public string? DetailType { get; init; }

        [JsonPropertyName("source")]
        public string? Source { get; init; }

        [JsonPropertyName("time")]
        public string? Time { get; init; }

        [JsonPropertyName("region")]
        public string? Region { get; init; }

        [JsonPropertyName("account")]
        public string? Account { get; init; }

        [JsonPropertyName("resources")]
        public List<string>? Resources { get; init; }
    }

    public HeroImageEventHandler()
    {
        // Initialize AWS S3 client for R2
        var r2AccountId = GetEnvironmentVariable("R2_ACCOUNT_ID");
        var r2AccessKeyId = GetEnvironmentVariable("R2_ACCESS_KEY_ID");
        var r2SecretAccessKey = GetEnvironmentVariable("R2_SECRET_ACCESS_KEY");
        var r2BucketName = GetEnvironmentVariable("R2_BUCKET_NAME");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            UseAccelerateEndpoint = false
        };

        var s3Client = new AmazonS3Client(
            r2AccessKeyId,
            r2SecretAccessKey,
            s3Config);

        // Initialize logger (Lambda runtime logs to CloudWatch)
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<HeroImageEventHandler>();

        // Initialize services
        var storageService = new R2StorageService(s3Client, r2BucketName, 
            loggerFactory.CreateLogger<R2StorageService>());
        var encodingService = new ImageEncodingService(
            loggerFactory.CreateLogger<ImageEncodingService>());
        _processingService = new HeroImageProcessingService(
            storageService,
            encodingService,
            loggerFactory.CreateLogger<HeroImageProcessingService>());

        _logger = logger;
    }

    /// <summary>
    /// Lambda handler entry point
    /// Receives EventBridge event and processes hero image
    /// </summary>
    public async Task<ImageProcessingResult> HandleAsync(EventBridgeEvent @event)
    {
        _logger.LogInformation(
            "Received HeroImageUpdated event: {DetailType} at {Time}",
            @event.DetailType,
            @event.Time);

        try
        {
            var heroEvent = @event.Detail;
            _logger.LogInformation(
                "Processing hero image - URL: {CanonicalUrl}, Key: {ImageKey}",
                heroEvent.CanonicalUrl,
                heroEvent.ImageKey);

            var result = await _processingService.ProcessHeroImageAsync(heroEvent);

            _logger.LogInformation(
                "Hero image processing completed successfully - " +
                "ImageId: {ImageId}, SuccessfulVariants: {SuccessfulCount}",
                result.ImageId,
                result.SuccessfulVariants.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error processing HeroImageUpdated event");

            // Re-throw to trigger Lambda retry via DLQ
            throw;
        }
    }

    private static string GetEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{name}' not set");
        }

        return value;
    }
}
