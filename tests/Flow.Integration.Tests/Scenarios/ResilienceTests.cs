using System.Net.Http.Json;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Infrastructure.Assistant;
using Flow.Infrastructure.Notifications;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Covers the behaviour that only shows up when an external dependency misbehaves.
/// </summary>
public class CircuitBreakerTests
{
    /// <summary>
    /// The cooldown is a whole minute and the clock is moved by hand.
    ///
    /// These tests used to run on wall-clock windows of a few dozen milliseconds and lost
    /// the race on a loaded CI runner: the cooldown elapsed between opening the circuit and
    /// asserting it was open, the breaker went half-open exactly as designed, and a correct
    /// implementation reported a failure that was really a scheduling delay.
    /// </summary>
    private static (CircuitBreaker Breaker, TestTimeProvider Clock) Create(
        int threshold = 3, TimeSpan? cooldown = null)
    {
        var clock = new TestTimeProvider();

        var breaker = new CircuitBreaker(
            Options.Create(new GeminiOptions
            {
                CircuitBreakerFailureThreshold = threshold,
                CircuitBreakerCooldown = cooldown ?? TimeSpan.FromMinutes(1)
            }),
            NullLogger<CircuitBreaker>.Instance,
            clock);

        return (breaker, clock);
    }

    [Fact]
    public void StaysClosedWhileCallsSucceed()
    {
        var (breaker, _) = Create();

        for (var i = 0; i < 10; i++)
        {
            breaker.AllowRequest().Should().BeTrue();
            breaker.RecordSuccess();
        }

        breaker.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void OpensAfterConsecutiveFailures()
    {
        var (breaker, _) = Create(threshold: 3);

        for (var i = 0; i < 3; i++) breaker.RecordFailure();

        breaker.IsOpen.Should().BeTrue();
        breaker.AllowRequest().Should().BeFalse(
            because: "a provider that is clearly down should cost milliseconds, not a full timeout each");
    }

    [Fact]
    public void ASuccessResetsTheFailureRun()
    {
        var (breaker, _) = Create(threshold: 3);

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();
        breaker.RecordFailure();

        breaker.IsOpen.Should().BeFalse(because: "the failures were not consecutive");
    }

    [Fact]
    public void AfterTheCooldown_OneTrialRequestIsAllowedThrough()
    {
        var (breaker, clock) = Create(threshold: 2);

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.AllowRequest().Should().BeFalse();

        clock.Advance(TimeSpan.FromMinutes(2));

        breaker.AllowRequest().Should().BeTrue(because: "the circuit goes half-open");

        // If the trial fails, it closes again immediately rather than after another full run.
        breaker.RecordFailure();
        breaker.AllowRequest().Should().BeFalse();
    }

    [Fact]
    public void ARecoveredProviderClosesTheCircuit()
    {
        var (breaker, clock) = Create(threshold: 2);

        breaker.RecordFailure();
        breaker.RecordFailure();
        clock.Advance(TimeSpan.FromMinutes(2));

        breaker.AllowRequest().Should().BeTrue();
        breaker.RecordSuccess();

        breaker.IsOpen.Should().BeFalse();
        breaker.AllowRequest().Should().BeTrue();
    }
}

/// <summary>Outbox dispatch: retry, backoff, dead-lettering and honest reporting.</summary>
[Collection(MongoCollection.Name)]
public class OutboxDispatcherTests
{
    private readonly MongoFixture _mongo;

    public OutboxDispatcherTests(MongoFixture mongo) => _mongo = mongo;

    private sealed class StubPushSender : IPushNotificationSender
    {
        public PushDeliveryOutcome Outcome { get; set; } = PushDeliveryOutcome.Delivered;
        public bool Configured { get; set; } = true;
        public int Calls { get; private set; }
        public List<string> DedupeKeys { get; } = [];

        public bool IsConfigured => Configured;

        public Task<PushDeliveryResult> SendAsync(
            PushNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            DedupeKeys.Add(request.DedupeKey);

            return Task.FromResult(Outcome switch
            {
                PushDeliveryOutcome.Delivered => PushDeliveryResult.Delivered("provider-id"),
                PushDeliveryOutcome.TransientFailure => PushDeliveryResult.Transient("503"),
                PushDeliveryOutcome.PermanentFailure => PushDeliveryResult.Permanent("400 bad request"),
                _ => PushDeliveryResult.NotConfigured()
            });
        }
    }

    private FlowApiFactory CreateFactory(StubPushSender sender) =>
        new(_mongo, services =>
        {
            services.AddSingleton<IPushNotificationSender>(sender);
            // The hosted worker is driven explicitly by the tests instead of on its timer.
            services.Configure<OutboxOptions>(o => o.Enabled = false);
        });

    private static async Task<Guid> QueueANotificationAsync(FlowApiFactory factory)
    {
        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia que gera notificação",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = (Guid?)null
        });

        var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        return ideaId;
    }

    private static OutboxDispatcherHostedService Dispatcher(FlowApiFactory factory, int maxAttempts = 6) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }),
            NullLogger<OutboxDispatcherHostedService>.Instance);

    [Fact]
    public async Task DeliversPendingMessagesAndMarksThemDispatched()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender();
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);

        var dispatched = await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        dispatched.Should().BeGreaterThan(0);
        sender.Calls.Should().BeGreaterThan(0);

        var messages = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).ToListAsync();

        messages.Should().OnlyContain(m => m.Status == OutboxStatus.Dispatched);
        messages.Should().OnlyContain(m => m.DispatchedAt != null);
    }

    [Fact]
    public async Task ATransientFailureSchedulesARetryInsteadOfLosingTheMessage()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender { Outcome = PushDeliveryOutcome.TransientFailure };
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);
        await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        var message = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).FirstAsync();

        message.Status.Should().Be(OutboxStatus.Failed);
        message.AttemptCount.Should().Be(1);
        message.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow,
            because: "backoff pushes the retry into the future");
        message.LastError.Should().Contain("503");
    }

    [Fact]
    public async Task RepeatedTransientFailuresEventuallyDeadLetter()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender { Outcome = PushDeliveryOutcome.TransientFailure };
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);

        var dispatcher = Dispatcher(factory, maxAttempts: 3);
        var outbox = factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<IOutboxRepository>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            // Pull the retry forward so the test does not have to wait out the backoff.
            await factory.Mongo.NotificationOutbox.UpdateManyAsync(
                Builders<OutboxMessage>.Filter.Empty,
                Builders<OutboxMessage>.Update.Set(
                    m => m.NextAttemptAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

            await dispatcher.DrainOnceAsync(CancellationToken.None);
        }

        var message = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).FirstAsync();

        message.Status.Should().Be(OutboxStatus.DeadLettered);
        message.AttemptCount.Should().Be(3);

        await dispatcher.DrainOnceAsync(CancellationToken.None);
        var callsAfterDeadLetter = sender.Calls;

        await dispatcher.DrainOnceAsync(CancellationToken.None);
        sender.Calls.Should().Be(callsAfterDeadLetter,
            because: "a dead-lettered message is not picked up again");
    }

    [Fact]
    public async Task APermanentFailureIsNotRetried()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender { Outcome = PushDeliveryOutcome.PermanentFailure };
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);
        await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        var message = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).FirstAsync();

        message.Status.Should().Be(OutboxStatus.DeadLettered,
            because: "a malformed request or a rejected credential will not fix itself");
        message.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task WithoutCredentials_MessagesStayPendingRatherThanBeingFakedAsSent()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender { Configured = false, Outcome = PushDeliveryOutcome.NotConfigured };
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);
        var dispatched = await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        dispatched.Should().Be(0);

        var message = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).FirstAsync();

        message.Status.Should().Be(OutboxStatus.Pending,
            because: "nothing may claim a push that never happened");
        message.AttemptCount.Should().Be(0);

        // The in-app notification centre is unaffected by the missing provider.
        var notifications = await factory.Mongo.Notifications
            .Find(Builders<Notification>.Filter.Empty).ToListAsync();

        notifications.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DispatchIsIdempotentAcrossDrains()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new StubPushSender();
        using var factory = CreateFactory(sender);

        await QueueANotificationAsync(factory);

        var dispatcher = Dispatcher(factory);
        await dispatcher.DrainOnceAsync(CancellationToken.None);
        var callsAfterFirstDrain = sender.Calls;

        await dispatcher.DrainOnceAsync(CancellationToken.None);
        await dispatcher.DrainOnceAsync(CancellationToken.None);

        sender.Calls.Should().Be(callsAfterFirstDrain,
            because: "an already dispatched message must never be sent twice");

        sender.DedupeKeys.Should().OnlyHaveUniqueItems();
    }

    private sealed record Created(Guid Id);
}
