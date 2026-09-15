using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Flow.Infrastructure.Persistence.Mongo;

/// <summary>
/// BSON representation rules for the whole application.
///
/// Registration is global and one-shot in the driver, so this runs exactly once and must
/// run before the first serialisation. The choices here are documented in
/// docs/sprint-2/data-model.md; the short version:
///
///  - Guid as string: readable in mongosh and stable for deep links.
///  - decimal as Decimal128: financial values must not go through binary floating point.
///  - DateTimeOffset as UTC DateTime: keeps range queries and date aggregation operators
///    natural. The domain writes UtcNow everywhere, so no offset information is lost;
///    sub-millisecond precision is, and that is an accepted trade-off.
///  - enums as strings: documents stay self-describing and survive enum reordering.
/// </summary>
public static class MongoMapping
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;

            BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
            BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
            BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(
                new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
            BsonSerializer.RegisterSerializer(
                new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));

            var pack = new ConventionPack
            {
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
                new CamelCaseElementNameConvention()
            };
            ConventionRegistry.Register("flow-conventions", pack, _ => true);

            RegisterClassMaps();

            _registered = true;
        }
    }

    /// <summary>
    /// Entities expose private setters and private parameterless constructors so that
    /// invariants can only be entered through factory methods. AutoMap handles both, but
    /// the maps are registered eagerly here so a mapping problem surfaces at startup
    /// rather than on the first write.
    /// </summary>
    private static void RegisterClassMaps()
    {
        // AutoMap only walks members declared on the type itself, so Id — which lives on
        // BaseEntity — is resolved through the base class map. Registering the base first
        // makes that explicit; the driver's named-id convention then picks Id up and the
        // derived maps inherit it on freeze. Calling MapIdMember on a derived map instead
        // throws, because the member does not belong to that class.
        Map<BaseEntity>();

        Map<Idea>();
        Map<Project>();
        Map<StrategicGuideline>();
        Map<Result>();
        Map<IdeaComment>();
        Map<ProjectSnapshot>();
        Map<AuditLog>();
        Map<PointLedgerEntry>();
        Map<RefreshToken>();
        Map<Notification>();
        Map<OutboxMessage>();
        Map<AssistantRun>();
        Map<StrategicGuidelineHistoryEntry>();

        Map<FlowScore>();
        Map<FlowScoreComponents>();
        Map<ResultMeasurement>();
    }

    private static void Map<T>(Action<BsonClassMap<T>>? configure = null)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T))) return;

        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            configure?.Invoke(cm);
        });
    }
}
