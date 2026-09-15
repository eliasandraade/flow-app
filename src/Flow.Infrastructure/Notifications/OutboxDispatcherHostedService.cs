using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Notifications;

/// <summary>
/// Drains the notification outbox outside of any domain transaction.
///
/// This is what keeps push-provider availability from deciding whether an idea approval is
/// persisted: the domain writes the notification and the outbox row inside its own
/// transaction and commits, and delivery happens here afterwards, with its own retry
/// schedule.
///
/// It is written to run in more than one replica at a time. Messages are claimed, not
/// merely read, so two instances polling at the same instant cannot both take the same one.
/// The provider-side idempotency key is the second layer: even if a claim were somehow
/// duplicated, the same message carries the same key on every attempt.
/// </summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;

    /// <summary>
    /// Identifies this worker in the claims it takes. Diagnostic only — exclusivity comes
    /// from the atomic claim, never from this string.
    /// </summary>
    private readonly string _workerId =
        $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid():N}";

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcherHostedService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public string WorkerId => _workerId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox dispatcher is disabled by configuration.");
            return;
        }

        // Give the API a moment to finish starting before competing for the database.
        await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);

        using (var scope = _scopeFactory.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

            if (!sender.IsConfigured)
            {
                // Stated plainly and once. Messages stay queued rather than being marked
                // delivered, so nothing in the system claims a push that never happened.
                _logger.LogWarning(
                    "Push provider is not configured. The in-app notification centre keeps working; "
                    + "outbox messages remain pending until credentials are supplied.");
                return;
            }
        }

        _logger.LogInformation("Outbox dispatcher {WorkerId} started.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DrainOnceAsync(stoppingToken);

                // Back off only when there was nothing to do, so a burst drains quickly.
                if (dispatched == 0)
                    await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failure here must never kill the worker; the next tick tries again.
                _logger.LogError(ex, "Outbox dispatch cycle failed.");
                await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Outbox dispatcher {WorkerId} stopped.", _workerId);
    }

    /// <summary>Processes one batch. Exposed for tests so the loop does not have to be run.</summary>
    public async Task<int> DrainOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();
        var metrics = scope.ServiceProvider.GetService<IFlowMetrics>();

        var now = _time.GetUtcNow();

        // Claimed, not merely read: from here on these messages belong to this worker, and
        // no other replica will pick them up while the lease holds.
        var claimed = await outbox.ClaimDueAsync(
            _workerId, now, _options.LeaseDuration, _options.BatchSize, cancellationToken);

        if (claimed.Count == 0) return 0;

        var dispatched = 0;

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Captured before anything mutates the message: completing clears the lease in
            // memory, and this is the value the write has to be fenced against.
            var fence = message.LeaseToken
                ?? throw new InvalidOperationException(
                    "A claimed outbox message must carry a lease token.");

            var result = await sender.SendAsync(
                new PushNotificationRequest(
                    message.UserId, message.Title, message.Body, message.DeepLink,
                    DedupeKey: message.DedupeKey,
                    // Stable across every retry of this message, which is what makes the
                    // provider-side deduplication work at all.
                    DeliveryId: message.Id),
                cancellationToken);

            var completedAt = _time.GetUtcNow();

            switch (result.Outcome)
            {
                case PushDeliveryOutcome.Delivered:
                    message.MarkDispatched(completedAt);
                    break;

                case PushDeliveryOutcome.TransientFailure:
                    // Backoff with jitter, and a dead-letter once the attempts run out.
                    message.MarkFailed(
                        result.Error ?? "Transient failure", _options.MaxAttempts, completedAt);
                    break;

                case PushDeliveryOutcome.PermanentFailure:
                    // No point retrying a malformed request or a rejected credential.
                    message.MarkFailed(
                        result.Error ?? "Permanent failure", maxAttempts: 1, completedAt);
                    break;

                case PushDeliveryOutcome.NotConfigured:
                    // Leave this one exactly as it is: pending, and honest about it. The
                    // claim is handed back rather than left to lapse, and no attempt is
                    // counted, because nothing was actually tried.
                    message.ReleaseClaim();
                    await outbox.TryCompleteAsync(message, fence, cancellationToken);
                    return dispatched;
            }

            var written = await outbox.TryCompleteAsync(message, fence, cancellationToken);

            if (!written)
            {
                // The lease lapsed while the provider was being called, and another worker
                // has taken the message over. Its state is newer than ours and must not be
                // flattened by a conclusion we reached before losing the claim. What keeps
                // this from becoming a duplicate push is the other half of the defence: the
                // provider-side idempotency key is derived from the message id, so both
                // attempts carry the same one.
                _logger.LogWarning(
                    "Outbox message {MessageId} was completed by another worker while this one "
                    + "was delivering. The result of this attempt is discarded.",
                    message.Id);

                continue;
            }

            // Counted only once the write was accepted: an attempt whose result was
            // discarded did not happen as far as the system is concerned.
            switch (result.Outcome)
            {
                case PushDeliveryOutcome.Delivered:
                    metrics?.NotificationSent();
                    dispatched++;
                    break;
                case PushDeliveryOutcome.TransientFailure:
                    metrics?.NotificationFailed("transient");
                    break;
                case PushDeliveryOutcome.PermanentFailure:
                    metrics?.NotificationFailed("permanent");
                    break;
            }

            if (message.Status == Domain.Enums.OutboxStatus.DeadLettered)
            {
                _logger.LogError(
                    "Outbox message {MessageId} dead-lettered after {Attempts} attempts: {Error}",
                    message.Id, message.AttemptCount, message.LastError);
            }
        }

        return dispatched;
    }
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = OutboxMessage.DefaultMaxAttempts;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a claim is respected. Long enough to cover a slow provider call and its
    /// retries, short enough that a crashed worker does not hold a notification hostage.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
}
