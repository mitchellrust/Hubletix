# Webhook Error Handling Strategy

## Overview
Stripe webhooks require careful error handling because:
1. **Webhooks retry on failures** (up to 3 days with exponential backoff)
2. **Duplicate events can occur** (same event ID sent multiple times)
3. **Concurrent webhooks** may try to update the same record
4. **Database issues** should trigger retries, but logical issues shouldn't

## Error Handling Patterns

### 1. DbUpdateConcurrencyException
**When:** Another webhook/process updated the same record
**Action:** Log warning and **DON'T throw** (return 200)
**Reason:** The update already happened (idempotent operation)

```csharp
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(
        ex,
        "Concurrency conflict (likely duplicate webhook): Entity={Entity}",
        entityId
    );
    // Don't throw - webhook already processed successfully
}
```

### 2. DbUpdateException
**When:** Database constraint violation, connection issue, deadlock
**Action:** Log error and **RE-THROW** (return 500)
**Reason:** Transient issue - retry may succeed

```csharp
catch (DbUpdateException ex)
{
    _logger.LogError(
        ex,
        "Database error saving changes: Entity={Entity}",
        entityId
    );
    // Re-throw to trigger Stripe webhook retry
    throw;
}
```

### 3. InvalidOperationException (Business Logic)
**When:** Invalid state, missing data, business rule violation
**Action:** Log error and **DON'T throw** (return 200)
**Reason:** Retrying won't fix it - needs manual investigation

```csharp
catch (InvalidOperationException ex)
{
    _logger.LogError(
        ex,
        "Business logic error processing webhook: Entity={Entity}",
        entityId
    );
    // Don't throw - retrying won't help
    // Alert/monitor this for manual investigation
}
```

### 4. General Exception
**When:** Unexpected errors
**Action:** Log error and **return 200 in outer handler**
**Reason:** Prevent retry storms for unknown issues

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error processing webhook");
    // Outer handler returns 200 to prevent retry storms
    return Ok();
}
```

## Implementation in StripePlatformWebhookController

### Outer Handler Pattern
```csharp
public async Task<IActionResult> HandleWebhook()
{
    try
    {
        // Verify signature, parse event
        // Call specific handlers
        return Ok();
    }
    catch (StripeException ex)
    {
        // Signature verification failed
        _logger.LogError(ex, "Webhook verification failed");
        return BadRequest(); // Don't retry invalid signatures
    }
    catch (Exception ex)
    {
        // Catch-all for unexpected errors
        _logger.LogError(ex, "Error processing webhook");
        return Ok(); // Return 200 to prevent retry storms
    }
}
```

### Inner Handler Pattern (Each Event Type)
```csharp
private async Task HandleSubscriptionUpdated(Event stripeEvent)
{
    var subscription = stripeEvent.Data.Object as Subscription;
    // ... validation and early returns ...
    
    try
    {
        // Make database changes
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Successfully saved changes");
    }
    catch (DbUpdateConcurrencyException ex)
    {
        // Duplicate webhook - already processed
        _logger.LogWarning(ex, "Concurrency conflict");
        // Don't throw - idempotent
    }
    catch (DbUpdateException ex)
    {
        // Database issue - should retry
        _logger.LogError(ex, "Database error");
        throw; // Trigger retry
    }
}
```

## Idempotency Considerations

### Using Event IDs
Store processed event IDs to detect duplicates:

```csharp
// Check if event already processed
var existingEvent = await _dbContext.WebhookEvents
    .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id);
    
if (existingEvent != null)
{
    _logger.LogInformation(
        "Event already processed: {EventId}",
        stripeEvent.Id
    );
    return; // Skip processing
}

// Process event...

// Record event as processed
_dbContext.WebhookEvents.Add(new WebhookEvent
{
    StripeEventId = stripeEvent.Id,
    EventType = stripeEvent.Type,
    ProcessedAt = DateTime.UtcNow
});

await _dbContext.SaveChangesAsync();
```

### Using Unique Constraints
Rely on database constraints for natural idempotency:

```csharp
// If StripeSubscriptionId has unique constraint
// Duplicate inserts will fail with DbUpdateException
// But concurrency conflicts are OK for updates
```

## Transaction Usage

### When to Use Transactions
- Multiple related tables updated
- Need atomic all-or-nothing behavior
- Critical activation logic

```csharp
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try
{
    // Multiple updates...
    await _dbContext.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch (DbUpdateException ex)
{
    await transaction.RollbackAsync();
    _logger.LogError(ex, "Transaction failed");
    throw; // Trigger retry
}
```

### When NOT to Use Transactions
- Simple single-table updates
- Read-only operations
- Operations that call external APIs (could timeout)

## Monitoring & Alerting

### Metrics to Track
1. **Webhook processing time** - Detect slow handlers
2. **Retry count** - High retries indicate issues
3. **Concurrency conflict rate** - Expected but monitor spikes
4. **DbUpdateException rate** - Database health indicator
5. **Unprocessed events** - Events that failed all retries

### Logging Best Practices
```csharp
// Always log with context
_logger.LogInformation(
    "Processing webhook: EventId={EventId}, Type={EventType}, SubscriptionId={SubscriptionId}",
    stripeEvent.Id,
    stripeEvent.Type,
    subscription.Id
);

// Log successes (helps debugging)
_logger.LogInformation(
    "Successfully processed webhook: EventId={EventId}",
    stripeEvent.Id
);

// Log warnings for expected issues
_logger.LogWarning(
    "Duplicate webhook detected: EventId={EventId}",
    stripeEvent.Id
);

// Log errors with full context
_logger.LogError(
    ex,
    "Failed to process webhook: EventId={EventId}, Type={EventType}",
    stripeEvent.Id,
    stripeEvent.Type
);
```

## Testing Strategy

### Unit Tests
- Test each handler with mock DbContext
- Verify correct exception handling
- Test idempotency (calling handler twice)

### Integration Tests
- Use Stripe CLI to send test webhooks
- Verify database state after processing
- Test duplicate event handling
- Test concurrent webhook processing

### Stripe CLI Commands
```bash
# Listen to webhooks
stripe listen --forward-to localhost:9000/api/webhooks/stripe/platform

# Trigger test events
stripe trigger checkout.session.completed
stripe trigger invoice.paid
stripe trigger customer.subscription.updated
stripe trigger customer.subscription.deleted

# Send specific event
stripe events resend evt_xxx
```

## Common Pitfalls

### ❌ DON'T: Throw on every error
```csharp
catch (Exception ex)
{
    throw; // This causes retry storms!
}
```

### ✅ DO: Differentiate retryable vs non-retryable
```csharp
catch (DbUpdateException ex)
{
    throw; // Retryable
}
catch (InvalidOperationException ex)
{
    // Don't throw - needs manual investigation
}
```

### ❌ DON'T: Long-running operations
```csharp
// Webhook times out after 30 seconds!
await SendEmailToAllUsers(); // Could take minutes
```

### ✅ DO: Queue background jobs
```csharp
// Queue for background processing
await _backgroundJobQueue.QueueAsync(new SendWelcomeEmailJob
{
    UserId = user.Id
});
```

### ❌ DON'T: Call external APIs without timeout
```csharp
var response = await httpClient.GetAsync(url); // No timeout!
```

### ✅ DO: Set timeouts
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var response = await httpClient.GetAsync(url, cts.Token);
```

## Summary

**Return 200 (OK) for:**
- ✅ Successfully processed
- ✅ Duplicate webhook (already processed)
- ✅ Business logic errors (retry won't help)
- ✅ Validation errors (invalid data)

**Return 500 (Error) for:**
- ❌ Database connection issues
- ❌ Deadlocks
- ❌ Transient failures that might succeed on retry

**Return 400 (Bad Request) for:**
- ❌ Invalid webhook signature
- ❌ Malformed event data

This ensures webhooks retry when they should, but don't retry when they shouldn't!
