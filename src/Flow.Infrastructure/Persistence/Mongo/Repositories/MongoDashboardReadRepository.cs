using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Flow.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Aggregation pipelines behind the executive dashboard.
///
/// Each collection is visited once with a $facet, so the whole dashboard costs a handful
/// of round trips instead of one per metric. Adding a metric to an existing facet is free;
/// that is the point of the shape.
/// </summary>
public sealed class MongoDashboardReadRepository : IDashboardReadRepository
{
    private const int AtRiskHorizonDays = 14;
    private const int BehindScheduleProgressThreshold = 75;

    private readonly FlowMongoContext _context;

    public MongoDashboardReadRepository(FlowMongoContext context) => _context = context;

    public async Task<IdeaAggregates> GetIdeaAggregatesAsync(CancellationToken cancellationToken = default)
    {
        var facet = new BsonDocument("$facet", new BsonDocument
        {
            ["total"] = new BsonArray { new BsonDocument("$count", "n") },
            ["byStatus"] = Group("$status"),
            ["byPriority"] = Group("$priority"),
            ["topByFlowScore"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("flowScore.total", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$sort", new BsonDocument("flowScore.total", -1)),
                new BsonDocument("$limit", 10),
                new BsonDocument("$project", new BsonDocument
                {
                    ["_id"] = 1,
                    ["title"] = 1,
                    ["status"] = 1,
                    ["score"] = 1,
                    ["flowScoreTotal"] = "$flowScore.total",
                    ["submittedByName"] = 1
                })
            },
            ["byGuideline"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("linkedGuidelineId", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$linkedGuidelineId",
                    ["n"] = new BsonDocument("$sum", 1)
                })
            }
        });

        var doc = await RunFacetAsync(_context.Ideas, facet, cancellationToken);

        return new IdeaAggregates(
            Total: ReadCount(doc, "total"),
            ByStatus: ReadStatusCounts(doc, "byStatus"),
            ByPriority: ReadStatusCounts(doc, "byPriority"),
            TopByFlowScore: doc["topByFlowScore"].AsBsonArray
                .Select(x => x.AsBsonDocument)
                .Select(d => new RankedIdeaRow(
                    Guid.Parse(d["_id"].AsString),
                    d.GetValue("title", "").AsString,
                    d.GetValue("status", "").AsString,
                    NullableInt(d, "score"),
                    NullableInt(d, "flowScoreTotal"),
                    d.GetValue("submittedByName", "").AsString))
                .ToList(),
            CountByGuideline: doc["byGuideline"].AsBsonArray
                .Select(x => x.AsBsonDocument)
                .ToDictionary(d => Guid.Parse(d["_id"].AsString), d => d["n"].ToInt32()));
    }

    public async Task<ProjectAggregates> GetProjectAggregatesAsync(
        DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var now = at.UtcDateTime;
        var horizon = now.AddDays(AtRiskHorizonDays);
        var openStatuses = new BsonArray { "Planned", "InProgress", "Blocked" };

        var projectFields = new BsonDocument
        {
            ["_id"] = 1,
            ["title"] = 1,
            ["ownerId"] = 1,
            ["ownerName"] = 1,
            ["status"] = 1,
            ["stage"] = 1,
            ["progressPercentage"] = 1,
            ["deadline"] = 1,
            ["blockedReason"] = 1,
            ["blockedSince"] = 1
        };

        var facet = new BsonDocument("$facet", new BsonDocument
        {
            ["total"] = new BsonArray { new BsonDocument("$count", "n") },
            ["byStatus"] = Group("$status"),
            ["byStage"] = Group("$stage"),
            ["converted"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("sourceIdeaId", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$count", "n")
            },
            ["avgProgress"] = new BsonArray
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["v"] = new BsonDocument("$avg", "$progressPercentage")
                })
            },
            // Completion time in days, computed server-side from the millisecond delta.
            ["completionDays"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument
                {
                    ["status"] = "Completed",
                    ["startDate"] = new BsonDocument("$ne", BsonNull.Value),
                    ["completedAt"] = new BsonDocument("$ne", BsonNull.Value)
                }),
                new BsonDocument("$project", new BsonDocument("days",
                    new BsonDocument("$divide", new BsonArray
                    {
                        new BsonDocument("$subtract", new BsonArray { "$completedAt", "$startDate" }),
                        86400000.0
                    }))),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["v"] = new BsonDocument("$avg", "$days")
                })
            },
            ["blocked"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("status", "Blocked")),
                new BsonDocument("$project", projectFields)
            },
            ["overdue"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument
                {
                    ["status"] = new BsonDocument("$in", openStatuses),
                    ["deadline"] = new BsonDocument("$lt", now)
                }),
                new BsonDocument("$project", projectFields)
            },
            // At risk: deadline inside the horizon, not yet overdue, and either stuck or
            // clearly behind. Mirrors Project.IsAtRiskAt so the API and the dashboard
            // cannot disagree about which projects are in trouble.
            ["atRisk"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument
                {
                    ["status"] = new BsonDocument("$in", openStatuses),
                    ["deadline"] = new BsonDocument { ["$gte"] = now, ["$lte"] = horizon },
                    ["$or"] = new BsonArray
                    {
                        new BsonDocument("status", "Blocked"),
                        new BsonDocument("progressPercentage",
                            new BsonDocument("$lt", BehindScheduleProgressThreshold))
                    }
                }),
                new BsonDocument("$project", projectFields)
            },
            ["byGuideline"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("linkedGuidelineId", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$linkedGuidelineId",
                    ["total"] = new BsonDocument("$sum", 1),
                    ["completed"] = new BsonDocument("$sum",
                        new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$status", "Completed" }), 1, 0
                        }))
                })
            }
        });

        var doc = await RunFacetAsync(_context.Projects, facet, cancellationToken);

        return new ProjectAggregates(
            Total: ReadCount(doc, "total"),
            ByStatus: ReadStatusCounts(doc, "byStatus"),
            ByStage: ReadStatusCounts(doc, "byStage"),
            ConvertedFromIdeas: ReadCount(doc, "converted"),
            AverageProgress: ReadScalar(doc, "avgProgress"),
            AverageCompletionDays: ReadScalar(doc, "completionDays"),
            Blocked: doc["blocked"].AsBsonArray.Select(ToBlockedRow).ToList(),
            AtRisk: doc["atRisk"].AsBsonArray.Select(x => ToRiskRow(x, isOverdue: false)).ToList(),
            Overdue: doc["overdue"].AsBsonArray.Select(x => ToRiskRow(x, isOverdue: true)).ToList(),
            ByGuideline: doc["byGuideline"].AsBsonArray
                .Select(x => x.AsBsonDocument)
                .ToDictionary(
                    d => Guid.Parse(d["_id"].AsString),
                    d => new ProjectGuidelineCounts(d["total"].ToInt32(), d["completed"].ToInt32())));
    }

    public async Task<ResultAggregates> GetResultAggregatesAsync(CancellationToken cancellationToken = default)
    {
        var facet = new BsonDocument("$facet", new BsonDocument
        {
            ["estimated"] = MeasurementGroup("estimated"),
            ["actual"] = MeasurementGroup("actual"),
            ["payback"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("paybackPeriodMonths", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["v"] = new BsonDocument("$avg", "$paybackPeriodMonths")
                })
            },
            ["impact"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("$or", new BsonArray
                {
                    new BsonDocument("productivityGainPercent", new BsonDocument("$ne", BsonNull.Value)),
                    new BsonDocument("timeSavedHours", new BsonDocument("$ne", BsonNull.Value)),
                    new BsonDocument("qualityGainPercent", new BsonDocument("$ne", BsonNull.Value))
                })),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["productivity"] = new BsonDocument("$avg", "$productivityGainPercent"),
                    ["hours"] = new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { "$timeSavedHours", 0 })),
                    ["quality"] = new BsonDocument("$avg", "$qualityGainPercent"),
                    ["n"] = new BsonDocument("$sum", 1)
                })
            },
            ["netByProject"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("actual", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$project", new BsonDocument
                {
                    ["projectId"] = 1,
                    ["net"] = NetValueExpression("actual")
                })
            }
        });

        var doc = await RunFacetAsync(_context.Results, facet, cancellationToken);

        var estimated = doc["estimated"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var actual = doc["actual"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var impact = doc["impact"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;

        return new ResultAggregates(
            EstimatedRevenue: Decimal(estimated, "revenue"),
            EstimatedSavings: Decimal(estimated, "savings"),
            EstimatedCost: Decimal(estimated, "cost"),
            EstimatedRoiAverage: NullableDecimal(estimated, "roiAvg"),
            ProjectsWithEstimated: estimated?["n"].ToInt32() ?? 0,
            ActualRevenue: Decimal(actual, "revenue"),
            ActualSavings: Decimal(actual, "savings"),
            ActualCost: Decimal(actual, "cost"),
            ActualRoiAverage: NullableDecimal(actual, "roiAvg"),
            ProjectsWithActual: actual?["n"].ToInt32() ?? 0,
            AveragePaybackMonths: ReadNullableScalar(doc, "payback"),
            AverageProductivityGainPercent: NullableDecimal(impact, "productivity"),
            TotalTimeSavedHours: Decimal(impact, "hours"),
            AverageQualityGainPercent: NullableDecimal(impact, "quality"),
            ProjectsReportingImpact: impact?["n"].ToInt32() ?? 0,
            ActualNetValueByProject: doc["netByProject"].AsBsonArray
                .Select(x => x.AsBsonDocument)
                .ToDictionary(
                    d => Guid.Parse(d["projectId"].AsString),
                    d => ToDecimal(d.GetValue("net", 0))));
    }

    public async Task<IReadOnlyList<PeriodCounts>> GetMonthlyTrendsAsync(
        DateTimeOffset from, CancellationToken cancellationToken = default)
    {
        var since = from.UtcDateTime;
        var period = new BsonDocument("$dateToString",
            new BsonDocument { ["format"] = "%Y-%m", ["date"] = "$createdAt" });

        // $unionWith keeps the whole trend chart to a single round trip instead of one
        // aggregation per collection.
        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument("createdAt", new BsonDocument("$gte", since))),
            // $literal is required: a bare 0 in an inclusion projection means "exclude this
            // field", which MongoDB rejects when mixed with included fields.
            new("$project", new BsonDocument
            {
                ["period"] = period,
                ["kind"] = "idea",
                ["completed"] = new BsonDocument("$literal", 0)
            }),
            new("$unionWith", new BsonDocument
            {
                ["coll"] = "projects",
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("createdAt", new BsonDocument("$gte", since))),
                    new BsonDocument("$project", new BsonDocument
                    {
                        ["period"] = period,
                        ["kind"] = "project",
                        ["completed"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$status", "Completed" }), 1, 0
                        })
                    })
                }
            }),
            new("$group", new BsonDocument
            {
                ["_id"] = "$period",
                ["ideas"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$kind", "idea" }), 1, 0
                })),
                ["projects"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$kind", "project" }), 1, 0
                })),
                ["completed"] = new BsonDocument("$sum", "$completed")
            }),
            new("$sort", new BsonDocument("_id", 1))
        };

        var rows = await _context.Ideas
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return rows
            .Select(d => new PeriodCounts(
                d["_id"].AsString,
                d["ideas"].ToInt32(),
                d["projects"].ToInt32(),
                d["completed"].ToInt32()))
            .ToList();
    }

    public async Task<IReadOnlyList<RankedProjectRow>> GetTopProjectsByRealisedValueAsync(
        int take, CancellationToken cancellationToken = default)
    {
        // Ranking happens on results, then the top few are resolved against projects.
        // The $lookup runs on at most `take` documents, so it stays cheap.
        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument("actual", new BsonDocument("$ne", BsonNull.Value))),
            new("$project", new BsonDocument
            {
                ["projectId"] = 1,
                ["roi"] = "$actual.roi",
                ["net"] = NetValueExpression("actual")
            }),
            new("$sort", new BsonDocument("net", -1)),
            new("$limit", take),
            new("$lookup", new BsonDocument
            {
                ["from"] = "projects",
                ["localField"] = "projectId",
                ["foreignField"] = "_id",
                ["as"] = "project"
            }),
            new("$unwind", "$project"),
            new("$project", new BsonDocument
            {
                ["projectId"] = 1,
                ["roi"] = 1,
                ["net"] = 1,
                ["title"] = "$project.title",
                ["status"] = "$project.status",
                ["progressPercentage"] = "$project.progressPercentage"
            })
        };

        var rows = await _context.Results
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return rows
            .Select(d => new RankedProjectRow(
                Guid.Parse(d["projectId"].AsString),
                d.GetValue("title", "").AsString,
                d.GetValue("status", "").AsString,
                d.GetValue("progressPercentage", 0).ToInt32(),
                d.GetValue("roi", BsonNull.Value) is var roi && roi.IsBsonNull ? null : ToDecimal(roi),
                ToDecimal(d.GetValue("net", 0))))
            .ToList();
    }

    public async Task<ProjectDetailAggregates> GetProjectDetailAggregatesAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _context.ProjectSnapshots
            .Find(Builders<ProjectSnapshot>.Filter.Eq(x => x.ProjectId, projectId))
            .Sort(Builders<ProjectSnapshot>.Sort.Ascending(x => x.TakenAt))
            .Project(x => new { x.TriggerAction, x.TakenAt })
            .ToListAsync(cancellationToken);

        var auditCount = await _context.AuditLogs.CountDocumentsAsync(
            Builders<AuditLog>.Filter.And(
                Builders<AuditLog>.Filter.Eq(x => x.EntityType, nameof(Project)),
                Builders<AuditLog>.Filter.Eq(x => x.EntityId, projectId)),
            cancellationToken: cancellationToken);

        // Total time spent blocked is reconstructed by pairing each Blocked snapshot with
        // the transition that followed it.
        var timesBlocked = 0;
        var totalDaysBlocked = 0.0;
        DateTimeOffset? blockedAt = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.TriggerAction == "Blocked")
            {
                timesBlocked++;
                blockedAt = snapshot.TakenAt;
            }
            else if (blockedAt is not null)
            {
                totalDaysBlocked += (snapshot.TakenAt - blockedAt.Value).TotalDays;
                blockedAt = null;
            }
        }

        if (blockedAt is not null)
            totalDaysBlocked += (DateTimeOffset.UtcNow - blockedAt.Value).TotalDays;

        return new ProjectDetailAggregates(
            snapshots.Count, (int)auditCount, timesBlocked, totalDaysBlocked);
    }

    public async Task<StrategyDetailAggregates> GetStrategyDetailAggregatesAsync(
        Guid guidelineId, CancellationToken cancellationToken = default)
    {
        var ideaPipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument("linkedGuidelineId", guidelineId.ToString())),
            new("$group", new BsonDocument
            {
                ["_id"] = "$status",
                ["n"] = new BsonDocument("$sum", 1)
            })
        };

        var ideaRows = await _context.Ideas
            .Aggregate<BsonDocument>(ideaPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var projectPipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument("linkedGuidelineId", guidelineId.ToString())),
            new("$group", new BsonDocument
            {
                ["_id"] = "$status",
                ["n"] = new BsonDocument("$sum", 1),
                ["ids"] = new BsonDocument("$push", "$_id")
            })
        };

        var projectRows = await _context.Projects
            .Aggregate<BsonDocument>(projectPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var projectIds = projectRows
            .SelectMany(d => d["ids"].AsBsonArray.Select(v => v.AsString))
            .ToList();

        decimal estimatedNet = 0m, actualNet = 0m;

        if (projectIds.Count > 0)
        {
            var valuePipeline = new BsonDocument[]
            {
                new("$match",
                    new BsonDocument("projectId", new BsonDocument("$in", new BsonArray(projectIds)))),
                new("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["estimated"] = new BsonDocument("$sum", NetValueExpression("estimated")),
                    ["actual"] = new BsonDocument("$sum", NetValueExpression("actual"))
                })
            };

            var valueRows = await _context.Results
                .Aggregate<BsonDocument>(valuePipeline, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);

            var row = valueRows.FirstOrDefault();
            if (row is not null)
            {
                estimatedNet = ToDecimal(row.GetValue("estimated", 0));
                actualNet = ToDecimal(row.GetValue("actual", 0));
            }
        }

        return new StrategyDetailAggregates(
            IdeaCount: ideaRows.Sum(d => d["n"].ToInt32()),
            IdeasByStatus: ideaRows.Select(d => new StatusCount(d["_id"].AsString, d["n"].ToInt32())).ToList(),
            ProjectCount: projectRows.Sum(d => d["n"].ToInt32()),
            ProjectsByStatus: projectRows.Select(d => new StatusCount(d["_id"].AsString, d["n"].ToInt32())).ToList(),
            ActualNetValue: actualNet,
            EstimatedNetValue: estimatedNet);
    }

    // ---------- pipeline helpers ----------

    private static BsonArray Group(string field) =>
    [
        new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = field,
            ["n"] = new BsonDocument("$sum", 1)
        })
    ];

    private static BsonArray MeasurementGroup(string prefix) =>
    [
        new BsonDocument("$match", new BsonDocument(prefix, new BsonDocument("$ne", BsonNull.Value))),
        new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = BsonNull.Value,
            ["revenue"] = new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { $"${prefix}.revenue", 0 })),
            ["savings"] = new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { $"${prefix}.savings", 0 })),
            ["cost"] = new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { $"${prefix}.cost", 0 })),
            ["roiAvg"] = new BsonDocument("$avg", $"${prefix}.roi"),
            ["n"] = new BsonDocument("$sum", 1)
        })
    ];

    private static BsonDocument NetValueExpression(string prefix) =>
        new("$subtract", new BsonArray
        {
            new BsonDocument("$add", new BsonArray
            {
                new BsonDocument("$ifNull", new BsonArray { $"${prefix}.revenue", 0 }),
                new BsonDocument("$ifNull", new BsonArray { $"${prefix}.savings", 0 })
            }),
            new BsonDocument("$ifNull", new BsonArray { $"${prefix}.cost", 0 })
        });

    private static async Task<BsonDocument> RunFacetAsync<T>(
        IMongoCollection<T> collection, BsonDocument facet, CancellationToken cancellationToken)
    {
        var pipeline = new[] { facet };

        var result = await collection
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? new BsonDocument();
    }

    private static int ReadCount(BsonDocument doc, string facet)
    {
        if (!doc.TryGetValue(facet, out var value) || value.AsBsonArray.Count == 0) return 0;
        return value.AsBsonArray[0].AsBsonDocument.GetValue("n", 0).ToInt32();
    }

    private static double ReadScalar(BsonDocument doc, string facet) =>
        ReadNullableScalar(doc, facet) ?? 0.0;

    private static double? ReadNullableScalar(BsonDocument doc, string facet)
    {
        if (!doc.TryGetValue(facet, out var value) || value.AsBsonArray.Count == 0) return null;

        var v = value.AsBsonArray[0].AsBsonDocument.GetValue("v", BsonNull.Value);
        return v.IsBsonNull ? null : v.ToDouble();
    }

    private static IReadOnlyList<StatusCount> ReadStatusCounts(BsonDocument doc, string facet)
    {
        if (!doc.TryGetValue(facet, out var value)) return [];

        return value.AsBsonArray
            .Select(x => x.AsBsonDocument)
            .Where(d => !d["_id"].IsBsonNull)
            .Select(d => new StatusCount(d["_id"].AsString, d["n"].ToInt32()))
            .ToList();
    }

    private static BlockedProjectRow ToBlockedRow(BsonValue value)
    {
        var d = value.AsBsonDocument;
        var since = d.GetValue("blockedSince", BsonNull.Value);

        return new BlockedProjectRow(
            Guid.Parse(d["_id"].AsString),
            d.GetValue("title", "").AsString,
            Guid.Parse(d.GetValue("ownerId", Guid.Empty.ToString()).AsString),
            d.GetValue("ownerName", "").AsString,
            d.GetValue("blockedReason", "").IsBsonNull ? "" : d.GetValue("blockedReason", "").AsString,
            since.IsBsonNull ? null : new DateTimeOffset(since.ToUniversalTime(), TimeSpan.Zero));
    }

    private static RiskProjectRow ToRiskRow(BsonValue value, bool isOverdue)
    {
        var d = value.AsBsonDocument;
        var deadline = d.GetValue("deadline", BsonNull.Value);

        return new RiskProjectRow(
            Guid.Parse(d["_id"].AsString),
            d.GetValue("title", "").AsString,
            Guid.Parse(d.GetValue("ownerId", Guid.Empty.ToString()).AsString),
            d.GetValue("ownerName", "").AsString,
            d.GetValue("status", "").AsString,
            d.GetValue("stage", "").AsString,
            d.GetValue("progressPercentage", 0).ToInt32(),
            deadline.IsBsonNull ? null : new DateTimeOffset(deadline.ToUniversalTime(), TimeSpan.Zero),
            isOverdue);
    }

    private static int? NullableInt(BsonDocument d, string field)
    {
        var v = d.GetValue(field, BsonNull.Value);
        return v.IsBsonNull ? null : v.ToInt32();
    }

    private static decimal Decimal(BsonDocument? d, string field) =>
        d is null ? 0m : ToDecimal(d.GetValue(field, 0));

    private static decimal? NullableDecimal(BsonDocument? d, string field)
    {
        if (d is null) return null;
        var v = d.GetValue(field, BsonNull.Value);
        return v.IsBsonNull ? null : ToDecimal(v);
    }

    private static decimal ToDecimal(BsonValue value) => value.BsonType switch
    {
        BsonType.Decimal128 => (decimal)value.AsDecimal128,
        BsonType.Double => (decimal)value.AsDouble,
        BsonType.Int32 => value.AsInt32,
        BsonType.Int64 => value.AsInt64,
        BsonType.Null => 0m,
        _ => 0m
    };
}
