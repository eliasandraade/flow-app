using System.Net.Http.Json;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Infrastructure.Notifications;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// The outbox with more than one replica running.
///
/// A single dispatcher is easy: read what is due, send it, write the result. Two replicas
/// polling at the same instant both see the same Pending document before either writes, and
/// the recipient gets the notification twice. Everything here is about that window.
///
/// No sleeps. Lease expiry is driven by a clock the test moves, so the behaviour is
/// deterministic instead of merely probable.
/// </summary>
[Collection(MongoCollection.Name)]
public class OutboxConcurrencyTests
{
    private readonly MongoFixture _mongo;

    public OutboxConcurrencyTests(MongoFixture mongo) => _mongo = mongo;

    private sealed record Created(Guid Id);

    /// <summary>Records every delivery so a duplicate cannot hide.</summary>
    private sealed class RecordingSender : IPushNotificationSender
    {
        private readonly object _lock = new();

        public PushDeliveryOutcome Outcome { get; set; } = PushDeliveryOutcome.Delivered;
        public bool Configured { get; set; } = true;
        public List<Guid> DeliveryIds { get; } = [];
        public TimeSpan Latency { get; set; } = TimeSpan.Zero;

        public bool IsConfigured => Configured;

        public int CallsFor(Guid deliveryId)
        {
            lock (_lock) return DeliveryIds.Count(id => id == deliveryId);
        }

        public async Task<PushDeliveryResult> SendAsync(
            PushNotificationRequest request, CancellationToken cancellationToken = default)
        {
            lock (_lock) DeliveryIds.Add(request.DeliveryId);

            // Widens the window a real provider call would occupy, so a competing worker has
            // every chance to grab the same message if the claim were not exclusive.
            if (Latency > TimeSpan.Zero) await Task.Delay(Latency, cancellationToken);

            return Outcome switch
            {
                PushDeliveryOutcome.Delivered => PushDeliveryResult.Delivered("provider-id"),
                PushDeliveryOutcome.TransientFailure => PushDeliveryResult.Transient("503"),
                PushDeliveryOutcome.PermanentFailure => PushDeliveryResult.Permanent("400 bad request"),
                _ => PushDeliveryResult.NotConfigured()
            };
        }
    }

    private FlowApiFactory CreateFactory(RecordingSender sender) =>
        new(_mongo, services =>
        {
            services.AddSingleton<IPushNotificationSender>(sender);
            // The worker is driven explicitly by the tests instead of on its timer.
            services.Configure<OutboxOptions>(o => o.Enabled = false);
        });

    private static OutboxDispatcherHostedService Dispatcher(
        FlowApiFactory factory,
        TimeProvider? clock = null,
        int maxAttempts = 6,
        int batchSize = 50,
        TimeSpan? lease = null) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions
            {
                MaxAttempts = maxAttempts,
                BatchSize = batchSize,
                LeaseDuration = lease ?? TimeSpan.FromMinutes(2)
            }),
            NullLogger<OutboxDispatcherHostedService>.Instance,
            clock);

    /// <summary>Submitting an idea notifies every manager, which is what fills the outbox.</summary>
    private static async Task QueueNotificationsAsync(FlowApiFactory factory, int count = 1)
    {
        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        for (var i = 0; i < count; i++)
        {
            var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
            {
                title = $"Ideia {i} que gera notificação",
                description = "Descrição suficiente.",
                problem = "Problema suficiente.",
                linkedGuidelineId = (Guid?)null
            });

            var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
            await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);
        }
    }

    private static IOutboxRepository Outbox(FlowApiFactory factory, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
    }

    private static Task<List<OutboxMessage>> AllMessagesAsync(FlowApiFactory factory) =>
        factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).ToListAsync();

    // ─── The race ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoDispatchersRacingForOneMessage_ProduceExactlyOneDelivery()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        // A slow provider is the realistic shape of this race: the first worker is still
        // waiting on the network while the second one polls.
        var sender = new RecordingSender { Latency = TimeSpan.FromMilliseconds(150) };
        using var factory = CreateFactory(sender);

        await QueueNotificationsAsync(factory);

        var queued = await AllMessagesAsync(factory);
        queued.Should().HaveCount(1, "the test needs exactly one message to fight over");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> DrainAsync()
        {
            var dispatcher = Dispatcher(factory);
            await gate.Task;
            return await dispatcher.DrainOnceAsync(CancellationToken.None);
        }

        var first = Task.Run(DrainAsync);
        var second = Task.Run(DrainAsync);

        gate.SetResult();
        var dispatchedCounts = await Task.WhenAll(first, second);

        sender.DeliveryIds.Should().HaveCount(1,
            because: "the message is claimed, not merely read, so only one worker can send it");
        sender.CallsFor(queued[0].Id).Should().Be(1);

        dispatchedCounts.Count(count => count == 1).Should().Be(1);
        dispatchedCounts.Count(count => count == 0).Should().Be(1);

        var after = await AllMessagesAsync(factory);
        after.Should().ContainSingle().Which.Status.Should().Be(OutboxStatus.Dispatched);
    }

    [Fact]
    public async Task FourDispatchersOverManyMessages_DeliverEachExactlyOnce()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender { Latency = TimeSpan.FromMilliseconds(20) };
        using var factory = CreateFactory(sender);

        await QueueNotificationsAsync(factory, count: 12);

        var queued = await AllMessagesAsync(factory);
        queued.Should().HaveCount(12);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            var dispatcher = Dispatcher(factory, batchSize: 5);
            await gate.Task;

            // Several passes each, so the workers keep colliding rather than each taking a
            // clean slice on a single sweep.
            var total = 0;
            for (var pass = 0; pass < 4; pass++)
                total += await dispatcher.DrainOnceAsync(CancellationToken.None);

            return total;
        })).ToArray();

        gate.SetResult();
        var totals = await Task.WhenAll(workers);

        totals.Sum().Should().Be(12);
        sender.DeliveryIds.Should().HaveCount(12);
        sender.DeliveryIds.Distinct().Should().HaveCount(12,
            because: "no message may be delivered twice, no matter how the workers interleave");

        var after = await AllMessagesAsync(factory);
        after.Should().HaveCount(12).And.OnlyContain(m => m.Status == OutboxStatus.Dispatched);
    }

    // ─── The lease ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AMessageUnderAnActiveLeaseIsInvisibleToAnotherWorker()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(new RecordingSender());
        await QueueNotificationsAsync(factory);

        var repository = Outbox(factory, out var scope);
        using (scope)
        {
            var start = DateTimeOffset.UtcNow;

            var mine = await repository.ClaimDueAsync(
                "worker-a", start, TimeSpan.FromMinutes(5), limit: 10, CancellationToken.None);

            mine.Should().ContainSingle();
            mine[0].Status.Should().Be(OutboxStatus.Processing);
            mine[0].LeaseOwner.Should().Be("worker-a");

            // A minute later, still well inside the lease.
            var theirs = await repository.ClaimDueAsync(
                "worker-b", start.AddMinutes(1), TimeSpan.FromMinutes(5), limit: 10,
                CancellationToken.None);

            theirs.Should().BeEmpty(
                because: "a live claim is exclusive; taking it over would duplicate the push");
        }
    }

    [Fact]
    public async Task AnExpiredLeaseIsRecoveredByAnotherWorker()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(new RecordingSender());
        await QueueNotificationsAsync(factory);

        var repository = Outbox(factory, out var scope);
        using (scope)
        {
            var start = DateTimeOffset.UtcNow;

            // A worker claims the message and then, as far as anyone can tell, dies: the
            // pod is restarted, the process is killed, a deploy lands mid-batch.
            var abandoned = await repository.ClaimDueAsync(
                "worker-that-died", start, TimeSpan.FromMinutes(1), limit: 10, CancellationToken.None);

            abandoned.Should().ContainSingle();

            var recovered = await repository.ClaimDueAsync(
                "worker-that-survived", start.AddMinutes(2), TimeSpan.FromMinutes(1), limit: 10,
                CancellationToken.None);

            recovered.Should().ContainSingle(
                because: "a lapsed lease must not leave a notification stranded forever");
            recovered[0].Id.Should().Be(abandoned[0].Id);
            recovered[0].LeaseOwner.Should().Be("worker-that-survived");
        }
    }

    [Fact]
    public async Task ARecoveredMessageIsDeliveredExactlyOnceByTheWorkerThatTookItOver()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender();
        using var factory = CreateFactory(sender);
        await QueueNotificationsAsync(factory);

        var clock = new TestTimeProvider();

        // Simulate the crash: claim through the repository and never come back.
        var repository = Outbox(factory, out var scope);
        using (scope)
        {
            await repository.ClaimDueAsync(
                "worker-that-died", clock.GetUtcNow(), TimeSpan.FromMinutes(1), limit: 10,
                CancellationToken.None);
        }

        // Before the lease lapses, nobody else touches it.
        clock.Advance(TimeSpan.FromSeconds(30));
        (await Dispatcher(factory, clock).DrainOnceAsync(CancellationToken.None)).Should().Be(0);
        sender.DeliveryIds.Should().BeEmpty();

        // After it lapses, the work resumes.
        clock.Advance(TimeSpan.FromMinutes(2));
        (await Dispatcher(factory, clock).DrainOnceAsync(CancellationToken.None)).Should().Be(1);

        sender.DeliveryIds.Should().HaveCount(1);

        var message = (await AllMessagesAsync(factory)).Single();
        message.Status.Should().Be(OutboxStatus.Dispatched);
        message.LeaseOwner.Should().BeNull(because: "the claim ends with the attempt");
        message.LeaseExpiresAt.Should().BeNull();
    }

    // ─── Outcomes still land where they should ──────────────────────────────

    [Fact]
    public async Task ATransientFailureReleasesTheClaimAndSchedulesARetry()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender { Outcome = PushDeliveryOutcome.TransientFailure };
        using var factory = CreateFactory(sender);
        await QueueNotificationsAsync(factory);

        var clock = new TestTimeProvider();
        await Dispatcher(factory, clock).DrainOnceAsync(CancellationToken.None);

        var message = (await AllMessagesAsync(factory)).Single();

        message.Status.Should().Be(OutboxStatus.Failed);
        message.AttemptCount.Should().Be(1);
        message.LeaseOwner.Should().BeNull(because: "a failed attempt must not keep holding the claim");
        message.NextAttemptAt.Should().BeAfter(clock.GetUtcNow(),
            because: "backoff pushes the retry into the future");
    }

    [Fact]
    public async Task APermanentFailureDeadLettersWithoutBlockingTheQueue()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender { Outcome = PushDeliveryOutcome.PermanentFailure };
        using var factory = CreateFactory(sender);
        await QueueNotificationsAsync(factory);

        await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        var message = (await AllMessagesAsync(factory)).Single();

        message.Status.Should().Be(OutboxStatus.DeadLettered);
        message.LeaseOwner.Should().BeNull();

        // And it is not picked up again on the next sweep.
        (await Dispatcher(factory).DrainOnceAsync(CancellationToken.None)).Should().Be(0);
        sender.DeliveryIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task RepeatedTransientFailuresEventuallyDeadLetterAndStopBeingClaimed()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender { Outcome = PushDeliveryOutcome.TransientFailure };
        using var factory = CreateFactory(sender);
        await QueueNotificationsAsync(factory);

        var clock = new TestTimeProvider();

        // Each pass moves the clock past the backoff instead of waiting it out.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            clock.Advance(TimeSpan.FromHours(1));
            await Dispatcher(factory, clock, maxAttempts: 3).DrainOnceAsync(CancellationToken.None);
        }

        var message = (await AllMessagesAsync(factory)).Single();

        message.Status.Should().Be(OutboxStatus.DeadLettered);
        message.AttemptCount.Should().Be(3);
        message.LeaseOwner.Should().BeNull();

        clock.Advance(TimeSpan.FromDays(1));
        (await Dispatcher(factory, clock, maxAttempts: 3).DrainOnceAsync(CancellationToken.None))
            .Should().Be(0, because: "a dead-lettered message is out of the rotation");
    }

    [Fact]
    public async Task WithoutCredentials_TheClaimIsHandedBackAndNothingIsClaimedAsSent()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender
        {
            Configured = false,
            Outcome = PushDeliveryOutcome.NotConfigured
        };

        using var factory = CreateFactory(sender);
        await QueueNotificationsAsync(factory);

        var dispatched = await Dispatcher(factory).DrainOnceAsync(CancellationToken.None);

        dispatched.Should().Be(0);

        var message = (await AllMessagesAsync(factory)).Single();

        message.Status.Should().Be(OutboxStatus.Pending,
            because: "nothing was attempted, so the message goes back exactly as it was");
        message.AttemptCount.Should().Be(0,
            because: "a missing credential is not a failed delivery attempt");
        message.DispatchedAt.Should().BeNull();
        message.LeaseOwner.Should().BeNull(
            because: "holding a claim we cannot act on would block the message until the lease lapsed");

        // And the very next sweep can still pick it up, without waiting out a lease.
        var repository = Outbox(factory, out var scope);
        using (scope)
        {
            var claimable = await repository.ClaimDueAsync(
                "another-worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), limit: 10,
                CancellationToken.None);

            claimable.Should().ContainSingle();
        }
    }
}
