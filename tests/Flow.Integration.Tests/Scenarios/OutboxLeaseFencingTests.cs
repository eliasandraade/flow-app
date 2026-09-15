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
/// What happens when a worker comes back after losing its claim.
///
/// Claiming atomically stops two workers from picking up the same message. It does not stop
/// the slower one from finishing: worker A claims, stalls on a provider call for longer than
/// its lease, worker B legitimately recovers the message and processes it, and then A wakes
/// up and writes the conclusion it reached a lifetime ago. If that write is addressed only
/// by id, it lands, and it flattens both B's result and B's lease.
///
/// The document carrying a lease owner does not help unless the write actually checks it,
/// and the owner alone is not sufficient either — the same worker can hold two different
/// claims of the same message over time. Hence a token minted per claim.
///
/// The clock here is moved by hand rather than slept through: lease expiry has to be
/// deterministic, not probable.
/// </summary>
[Collection(MongoCollection.Name)]
public class OutboxLeaseFencingTests
{
    private readonly MongoFixture _mongo;

    public OutboxLeaseFencingTests(MongoFixture mongo) => _mongo = mongo;

    private sealed record Created(Guid Id);

    private sealed class RecordingSender : IPushNotificationSender
    {
        private readonly object _lock = new();

        public List<Guid> DeliveryIds { get; } = [];
        public PushDeliveryOutcome Outcome { get; set; } = PushDeliveryOutcome.Delivered;
        public bool Configured { get; set; } = true;

        public bool IsConfigured => Configured;

        public Task<PushDeliveryResult> SendAsync(
            PushNotificationRequest request, CancellationToken cancellationToken = default)
        {
            lock (_lock) DeliveryIds.Add(request.DeliveryId);

            return Task.FromResult(Outcome switch
            {
                PushDeliveryOutcome.Delivered => PushDeliveryResult.Delivered("provider-id"),
                PushDeliveryOutcome.TransientFailure => PushDeliveryResult.Transient("503"),
                PushDeliveryOutcome.PermanentFailure => PushDeliveryResult.Permanent("400"),
                _ => PushDeliveryResult.NotConfigured()
            });
        }
    }

    private FlowApiFactory CreateFactory(RecordingSender sender) =>
        new(_mongo, services =>
        {
            services.AddSingleton<IPushNotificationSender>(sender);
            services.Configure<OutboxOptions>(o => o.Enabled = false);
        });

    private static OutboxDispatcherHostedService Dispatcher(
        FlowApiFactory factory, TimeProvider clock, TimeSpan lease, int maxAttempts = 6) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions
            {
                MaxAttempts = maxAttempts,
                BatchSize = 50,
                LeaseDuration = lease
            }),
            NullLogger<OutboxDispatcherHostedService>.Instance,
            clock);

    private static async Task QueueOneAsync(FlowApiFactory factory)
    {
        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia que gera notificação",
            description = "Descrição suficiente.",
            problem = "Problema suficiente.",
            linkedGuidelineId = (Guid?)null
        });

        var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);
    }

    private static Task<List<OutboxMessage>> AllAsync(FlowApiFactory factory) =>
        factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Empty).ToListAsync();

    private static IOutboxRepository Repository(FlowApiFactory factory, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
    }

    // ─── The race this exists for ───────────────────────────────────────────

    [Fact]
    public async Task AWorkerThatLostItsLeaseCannotOverwriteTheWorkerThatTookOver()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(new RecordingSender());
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();
        var repository = Repository(factory, out var scope);

        using (scope)
        {
            // A claims the message and, as far as anyone can tell, goes away.
            var slow = await repository.ClaimDueAsync(
                "worker-a", clock.GetUtcNow(), TimeSpan.FromMinutes(1), 10, CancellationToken.None);

            slow.Should().ContainSingle();
            var staleFence = slow[0].LeaseToken!.Value;

            // The lease lapses and B legitimately recovers it.
            clock.Advance(TimeSpan.FromMinutes(2));

            var recovered = await repository.ClaimDueAsync(
                "worker-b", clock.GetUtcNow(), TimeSpan.FromMinutes(5), 10, CancellationToken.None);

            recovered.Should().ContainSingle();
            recovered[0].LeaseToken.Should().NotBe(staleFence,
                because: "every claim mints a new token, which is what makes a stale write detectable");

            var freshFence = recovered[0].LeaseToken!.Value;

            // B finishes properly.
            recovered[0].MarkDispatched(clock.GetUtcNow());
            (await repository.TryCompleteAsync(recovered[0], freshFence, CancellationToken.None))
                .Should().BeTrue();

            // And now A wakes up and tries to save what it concluded before it stalled.
            slow[0].MarkFailed("stale conclusion from a worker that lost its claim", 6, clock.GetUtcNow());

            var accepted = await repository.TryCompleteAsync(
                slow[0], staleFence, CancellationToken.None);

            accepted.Should().BeFalse(
                because: "the claim it is fencing against is no longer the one on the document");
        }

        var stored = (await AllAsync(factory)).Single();

        stored.Status.Should().Be(OutboxStatus.Dispatched,
            because: "the state the current owner wrote must survive");
        stored.AttemptCount.Should().Be(0,
            because: "the stale worker's failed attempt must not be recorded against the message");
        stored.LastError.Should().BeNull();
    }

    [Fact]
    public async Task TheSameWorkerReclaimingLaterStillCannotUseItsOldToken()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(new RecordingSender());
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();
        var repository = Repository(factory, out var scope);

        using (scope)
        {
            // This is why the owner name is not enough to fence with: same worker, two
            // different claims, and a write from the first must still be rejected.
            var first = await repository.ClaimDueAsync(
                "worker-a", clock.GetUtcNow(), TimeSpan.FromMinutes(1), 10, CancellationToken.None);

            var oldFence = first[0].LeaseToken!.Value;

            clock.Advance(TimeSpan.FromMinutes(2));

            var second = await repository.ClaimDueAsync(
                "worker-a", clock.GetUtcNow(), TimeSpan.FromMinutes(5), 10, CancellationToken.None);

            second.Should().ContainSingle();
            second[0].LeaseOwner.Should().Be("worker-a");
            second[0].LeaseToken.Should().NotBe(oldFence);

            first[0].MarkDispatched(clock.GetUtcNow());

            (await repository.TryCompleteAsync(first[0], oldFence, CancellationToken.None))
                .Should().BeFalse(because: "a claim that ended is not the claim being held now");
        }
    }

    // ─── The ordinary paths still work ──────────────────────────────────────

    [Fact]
    public async Task PresentingATokenThatWasAlreadyClearedIsRefused()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = CreateFactory(new RecordingSender());
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();
        var repository = Repository(factory, out var scope);

        using (scope)
        {
            var claimed = await repository.ClaimDueAsync(
                "worker-a", clock.GetUtcNow(), TimeSpan.FromMinutes(5), 10, CancellationToken.None);

            claimed[0].MarkDispatched(clock.GetUtcNow());

            (await repository.TryCompleteAsync(
                claimed[0], claimed[0].LeaseToken ?? Guid.Empty, CancellationToken.None))
                .Should().BeFalse(because: "MarkDispatched clears the token in memory");
        }

        // The dispatcher captures the token before mutating, which is the supported path.
        var afterMutation = (await AllAsync(factory)).Single();
        afterMutation.Status.Should().Be(OutboxStatus.Processing);
    }

    [Fact]
    public async Task TheDispatcherCapturesTheTokenBeforeMutatingAndTheWriteLands()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender();
        using var factory = CreateFactory(sender);
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();

        var dispatched = await Dispatcher(factory, clock, TimeSpan.FromMinutes(5))
            .DrainOnceAsync(CancellationToken.None);

        dispatched.Should().Be(1);

        var stored = (await AllAsync(factory)).Single();
        stored.Status.Should().Be(OutboxStatus.Dispatched);
        stored.LeaseToken.Should().BeNull(because: "the claim ends with the attempt");
        stored.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task FailureWithAValidLeaseIsAcceptedAndSchedulesARetry()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender { Outcome = PushDeliveryOutcome.TransientFailure };
        using var factory = CreateFactory(sender);
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();

        await Dispatcher(factory, clock, TimeSpan.FromMinutes(5)).DrainOnceAsync(CancellationToken.None);

        var stored = (await AllAsync(factory)).Single();
        stored.Status.Should().Be(OutboxStatus.Failed);
        stored.AttemptCount.Should().Be(1);
        stored.LeaseToken.Should().BeNull();
        stored.NextAttemptAt.Should().BeAfter(clock.GetUtcNow());
    }

    [Fact]
    public async Task ReleaseWithAValidLeaseIsAcceptedAndCostsNoAttempt()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender
        {
            Configured = false,
            Outcome = PushDeliveryOutcome.NotConfigured
        };

        using var factory = CreateFactory(sender);
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();

        await Dispatcher(factory, clock, TimeSpan.FromMinutes(5)).DrainOnceAsync(CancellationToken.None);

        var stored = (await AllAsync(factory)).Single();
        stored.Status.Should().Be(OutboxStatus.Pending);
        stored.AttemptCount.Should().Be(0);
        stored.LeaseToken.Should().BeNull();
    }

    // ─── Defence in depth, because HTTP is not transactional ────────────────

    [Fact]
    public async Task ATakenOverMessageStillCarriesTheSameIdempotencyKeyToTheProvider()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        var sender = new RecordingSender();
        using var factory = CreateFactory(sender);
        await QueueOneAsync(factory);

        var clock = new TestTimeProvider();
        var lease = TimeSpan.FromMinutes(1);

        // One worker delivers but, in this scenario, its write is going to be too late.
        var first = Dispatcher(factory, clock, lease);
        await first.DrainOnceAsync(CancellationToken.None);

        // Force the situation the fencing exists for: put the message back into Processing
        // with a lease that has already lapsed, as if the first attempt had never returned.
        await factory.Mongo.NotificationOutbox.UpdateManyAsync(
            Builders<OutboxMessage>.Filter.Empty,
            Builders<OutboxMessage>.Update
                .Set(x => x.Status, OutboxStatus.Processing)
                .Set(x => x.LeaseOwner, "worker-that-vanished")
                .Set(x => x.LeaseToken, Guid.NewGuid())
                .Set(x => x.LeaseExpiresAt, clock.GetUtcNow().Subtract(TimeSpan.FromMinutes(5)))
                .Set(x => x.DispatchedAt, (DateTimeOffset?)null));

        clock.Advance(TimeSpan.FromMinutes(10));

        var second = Dispatcher(factory, clock, lease);
        await second.DrainOnceAsync(CancellationToken.None);

        sender.DeliveryIds.Should().HaveCount(2, "the message really was attempted twice");
        sender.DeliveryIds.Distinct().Should().ContainSingle(
            because: "both attempts carry the same idempotency key, so the provider "
                   + "deduplicates what the lease could not prevent — HTTP and the database "
                   + "cannot be made atomic together, so the defence has two layers");
    }
}
