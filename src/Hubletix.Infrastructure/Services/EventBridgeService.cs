using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Event detail for HeroImageUpdated events
/// </summary>
public record HeroImageUpdatedEventDetail
{
    /// <summary>
    /// Full canonical URL to the source image in R2
    /// </summary>
    public required string CanonicalUrl { get; init; }

    /// <summary>
    /// Unique image key/ID from the R2 object key path
    /// </summary>
    public required string ImageKey { get; init; }
}

/// <summary>
/// Service for publishing events to AWS EventBridge
/// </summary>
public interface IEventBridgeService
{
    /// <summary>
    /// Publish a HeroImageUpdated event to EventBridge
    /// </summary>
    Task PublishHeroImageUpdatedAsync(
        string canonicalUrl,
        string imageKey,
        CancellationToken cancellationToken = default);
}

public class EventBridgeService : IEventBridgeService
{
    private readonly IAmazonEventBridge _eventBridgeClient;
    private readonly string _eventBusName;
    private readonly ILogger<EventBridgeService> _logger;

    public EventBridgeService(
        IAmazonEventBridge eventBridgeClient,
        string eventBusName,
        ILogger<EventBridgeService> logger)
    {
        _eventBridgeClient = eventBridgeClient;
        _eventBusName = eventBusName;
        _logger = logger;
    }

    public async Task PublishHeroImageUpdatedAsync(
        string canonicalUrl,
        string imageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Publishing HeroImageUpdated event: CanonicalUrl={CanonicalUrl}, ImageKey={ImageKey}",
                canonicalUrl,
                imageKey);

            var heroEvent = new HeroImageUpdatedEventDetail
            {
                CanonicalUrl = canonicalUrl,
                ImageKey = imageKey
            };

            var putEventsRequest = new PutEventsRequest
            {
                Entries = new List<PutEventsRequestEntry>
                {
                    new PutEventsRequestEntry
                    {
                        EventBusName = _eventBusName,
                        Source = "hubletix.api",
                        DetailType = "HeroImageUpdated",
                        Detail = JsonSerializer.Serialize(heroEvent),
                        Time = DateTime.UtcNow
                    }
                }
            };

            var response = await _eventBridgeClient.PutEventsAsync(putEventsRequest, cancellationToken);

            if (response.FailedEntryCount > 0)
            {
                _logger.LogError(
                    "Failed to publish HeroImageUpdated event: {FailedCount} entries failed",
                    response.FailedEntryCount);

                foreach (var failure in response.Entries.Where(e => !string.IsNullOrEmpty(e.ErrorCode)))
                {
                    _logger.LogError(
                        "EventBridge error: {ErrorCode} - {ErrorMessage}",
                        failure.ErrorCode,
                        failure.ErrorMessage);
                }

                throw new InvalidOperationException(
                    $"Failed to publish event to EventBridge: {response.FailedEntryCount} entries failed");
            }

            _logger.LogInformation(
                "Successfully published HeroImageUpdated event: EventId={EventId}",
                response.Entries.FirstOrDefault()?.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing HeroImageUpdated event for image {ImageKey}",
                imageKey);
            throw;
        }
    }
}
