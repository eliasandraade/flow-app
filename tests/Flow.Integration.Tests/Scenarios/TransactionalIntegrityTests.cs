using System.Net.Http.Json;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Integration.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Flow.Integration.Tests.Scenarios;

/// <summary>
/// Proves the guarantee the whole governance model rests on: an aggregate change, its
/// audit entry and its project snapshot either all commit or none of them do.
///
/// Under EF Core this came free from a single SaveChanges. On MongoDB it depends on an
/// explicit multi-document transaction, which is exactly the kind of guarantee that
/// silently disappears during a migration unless something proves it is still there.
/// </summary>
[Collection(MongoCollection.Name)]
public class TransactionalIntegrityTests
{
    private readonly MongoFixture _mongo;

    public TransactionalIntegrityTests(MongoFixture mongo) => _mongo = mongo;

    /// <summary>Audit repository that writes normally until told to fail.</summary>
    private sealed class ExplodingAuditLogRepository : IAuditLogRepository
    {
        public static bool ShouldFail;

        private readonly IAuditLogRepository _inner;

        public ExplodingAuditLogRepository(FlowMongoContextAccessor accessor) => _inner = accessor.Inner;

        public Task AppendAsync(AuditLog entry, CancellationToken cancellationToken = default) =>
            ShouldFail
                ? throw new InvalidOperationException("Injected audit failure.")
                : _inner.AppendAsync(entry, cancellationToken);

        public Task AppendRangeAsync(IEnumerable<AuditLog> entries, CancellationToken cancellationToken = default) =>
            ShouldFail
                ? throw new InvalidOperationException("Injected audit failure.")
                : _inner.AppendRangeAsync(entries, cancellationToken);

        public Task<IReadOnlyList<AuditLog>> GetForEntityAsync(
            string entityType, Guid entityId, CancellationToken cancellationToken = default) =>
            _inner.GetForEntityAsync(entityType, entityId, cancellationToken);
    }

    /// <summary>Carries the real repository so the decorator can delegate to it.</summary>
    private sealed class FlowMongoContextAccessor
    {
        public FlowMongoContextAccessor(
            Flow.Infrastructure.Persistence.Mongo.FlowMongoContext context,
            Flow.Infrastructure.Persistence.Mongo.MongoSessionAccessor sessions) =>
            Inner = new Flow.Infrastructure.Persistence.Mongo.Repositories
                .MongoAuditLogRepository(context, sessions);

        public IAuditLogRepository Inner { get; }
    }

    [Fact]
    public async Task WhenTheAuditWriteFails_TheDomainChangeIsRolledBackToo()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        ExplodingAuditLogRepository.ShouldFail = false;

        using var factory = new FlowApiFactory(_mongo, services =>
        {
            services.AddScoped<FlowMongoContextAccessor>();
            services.AddScoped<IAuditLogRepository, ExplodingAuditLogRepository>();
        });

        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);
        var manager = await factory.CreateClientAsAsync(UserRole.Manager);

        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia sujeita a rollback",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = (Guid?)null
        });
        created.EnsureSuccessStatusCode();

        var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var beforeStatus = (await factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId)).FirstAsync()).Status;

        beforeStatus.Should().Be(IdeaStatus.UnderReview);

        // Now make the audit write blow up in the middle of the approval transaction.
        ExplodingAuditLogRepository.ShouldFail = true;

        try
        {
            var approve = await manager.PostAsJsonAsync($"/api/v1/ideas/{ideaId}/approve",
                new { managerComment = "Isto não deve persistir." });

            approve.IsSuccessStatusCode.Should().BeFalse(
                because: "the request must fail rather than half-commit");
        }
        finally
        {
            ExplodingAuditLogRepository.ShouldFail = false;
        }

        var afterIdea = await factory.Mongo.Ideas
            .Find(Builders<Idea>.Filter.Eq(i => i.Id, ideaId)).FirstAsync();

        afterIdea.Status.Should().Be(IdeaStatus.UnderReview,
            because: "a state transition without its audit entry must not survive");

        var ledger = await factory.Mongo.PointLedger
            .Find(Builders<PointLedgerEntry>.Filter.Eq(e => e.ReferenceId, ideaId))
            .ToListAsync();

        ledger.Should().BeEmpty(because: "the points award is part of the same unit of work");

        var notifications = await factory.Mongo.Notifications
            .Find(Builders<Notification>.Filter.Eq(n => n.Type, NotificationType.IdeaApproved))
            .ToListAsync();

        notifications.Should().BeEmpty(
            because: "no one should be told an idea was approved when it was not");
    }

    [Fact]
    public async Task ProjectTransition_CommitsAggregateAuditAndSnapshotTogether()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = new FlowApiFactory(_mongo);
        var (manager, managerId) = await factory.CreateClientWithUserAsync(UserRole.Manager);

        var created = await manager.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Projeto transacional",
            description = "Descrição",
            priority = nameof(ProjectPriority.High),
            ownerId = managerId,
            linkedGuidelineId = (Guid?)null,
            estimatedCost = 1_000m,
            deadline = (DateTimeOffset?)null
        });
        created.EnsureSuccessStatusCode();

        var projectId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        await manager.PostAsync($"/api/v1/projects/{projectId}/start", null);
        await manager.PostAsJsonAsync($"/api/v1/projects/{projectId}/block",
            new { reason = "Fornecedor atrasou" });

        var audits = await factory.Mongo.AuditLogs
            .Find(Builders<AuditLog>.Filter.Eq(a => a.EntityId, projectId))
            .ToListAsync();

        var snapshots = await factory.Mongo.ProjectSnapshots
            .Find(Builders<ProjectSnapshot>.Filter.Eq(s => s.ProjectId, projectId))
            .ToListAsync();

        audits.Select(a => a.Action).Should().BeEquivalentTo(["Created", "Started", "Blocked"]);
        snapshots.Should().HaveCount(3,
            because: "every project transition captures a full snapshot");

        snapshots.Should().OnlyContain(s => s.SchemaVersion == ProjectSnapshot.CurrentSchemaVersion);

        var blockedSnapshot = snapshots.Single(s => s.TriggerAction == "Blocked");
        blockedSnapshot.Status.Should().Be(ProjectStatus.Blocked);
        blockedSnapshot.BlockedReason.Should().Be("Fornecedor atrasou");

        audits.Should().OnlyContain(a => a.CorrelationId != null,
            because: "every governance record is tied back to the trace that produced it");
    }

    [Fact]
    public async Task Outbox_RefusesToEnqueueTheSameEventTwice()
    {
        if (!_mongo.IsAvailable) throw new InvalidOperationException(_mongo.UnavailableReason);

        using var factory = new FlowApiFactory(_mongo);
        var operatorClient = await factory.CreateClientAsAsync(UserRole.Operator);

        var created = await operatorClient.PostAsJsonAsync("/api/v1/ideas", new
        {
            title = "Ideia com outbox",
            description = "Descrição",
            problem = "Problema",
            linkedGuidelineId = (Guid?)null
        });
        var ideaId = (await created.Content.ReadFromJsonAsync<Created>())!.Id;

        await operatorClient.PostAsync($"/api/v1/ideas/{ideaId}/submit", null);

        var messages = await factory.Mongo.NotificationOutbox
            .Find(Builders<OutboxMessage>.Filter.Regex(
                m => m.DedupeKey, new MongoDB.Bson.BsonRegularExpression($"^IdeaSubmitted:{ideaId}")))
            .ToListAsync();

        messages.Should().ContainSingle();
        messages[0].Status.Should().Be(OutboxStatus.Pending);

        // A second insert with the same key must be refused by the unique index, which is
        // what stops a retry from producing a duplicate push.
        var duplicate = OutboxMessage.For(
            Notification.Create(messages[0].UserId, NotificationType.IdeaSubmitted, "t", "b"),
            messages[0].DedupeKey);

        var act = async () => await factory.Mongo.NotificationOutbox.InsertOneAsync(duplicate);

        await act.Should().ThrowAsync<MongoWriteException>();
    }

    private sealed record Created(Guid Id);
}
