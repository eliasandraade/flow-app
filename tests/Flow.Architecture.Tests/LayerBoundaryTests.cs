using System.Reflection;
using FluentAssertions;

namespace Flow.Architecture.Tests;

/// <summary>
/// The dependency rule, enforced instead of described.
///
/// Clean Architecture is a claim the README makes and the compiler mostly keeps: a project
/// reference is what actually stops Domain from seeing Infrastructure. "Mostly" is the
/// problem — a reference added in a hurry to make something compile is a one-line change
/// that no reviewer necessarily notices, and by the time it matters the coupling is spread
/// across a dozen files.
///
/// These are deliberately small and use nothing but reflection. A rules framework would be
/// a dependency to maintain for four assertions that the base class library already
/// answers.
/// </summary>
public class LayerBoundaryTests
{
    private static readonly Assembly Domain = typeof(Flow.Domain.Entities.Idea).Assembly;
    private static readonly Assembly Application = typeof(Flow.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Flow.Infrastructure.DependencyInjection).Assembly;

    private static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);

    // ─── Dependencies point inward ──────────────────────────────────────────

    [Theory]
    [InlineData("Flow.Application")]
    [InlineData("Flow.Infrastructure")]
    [InlineData("Flow.API")]
    public void DomainDependsOnNoOtherLayer(string forbidden)
    {
        // The innermost layer. Anything it reaches for stops being a detail and becomes part
        // of the model, which is how a domain ends up impossible to test or to move.
        ReferencesOf(Domain).Should().NotContain(forbidden,
            because: $"Flow.Domain must not reference {forbidden}; dependencies point inward");
    }

    [Theory]
    [InlineData("Flow.Infrastructure")]
    [InlineData("Flow.API")]
    public void ApplicationDependsOnNeitherInfrastructureNorTheApi(string forbidden)
    {
        ReferencesOf(Application).Should().NotContain(forbidden,
            because: $"Flow.Application must not reference {forbidden}; it declares interfaces "
                   + "and lets the outer layers implement them");
    }

    // ─── The database stays a detail ────────────────────────────────────────

    [Fact]
    public void ApplicationDoesNotReferenceTheMongoDriver()
    {
        // Use cases talk to interfaces this layer declares. The day a filter definition or a
        // BsonDocument appears in a handler, the storage choice has stopped being a choice.
        ReferencesOf(Application).Should().NotContain(
            name => name.StartsWith("MongoDB", StringComparison.Ordinal),
            because: "the driver belongs to Infrastructure; the Application layer must stay "
                   + "testable without a database");
    }

    [Fact]
    public void ApplicationDoesNotReferenceTheMongoDriverFromDomainEither()
    {
        ReferencesOf(Domain).Should().NotContain(
            name => name.StartsWith("MongoDB", StringComparison.Ordinal));
    }

    [Fact]
    public void NoApplicationSignatureMentionsAMongoSessionHandle()
    {
        // The transaction boundary is the place this leaks most naturally: it is genuinely
        // easier to hand the session around than to hide it behind IUnitOfWork. Once it
        // leaks, every handler that touches a transaction needs the driver to compile.
        var offenders = Application.GetTypes()
            .SelectMany(SignatureTypesOf)
            .Where(type => type.Name.Contains("ClientSessionHandle", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .Distinct()
            .ToArray();

        offenders.Should().BeEmpty(
            because: "IUnitOfWork exists precisely so the driver session never crosses into "
                   + "the Application layer");
    }

    /// <summary>
    /// Every type that appears in a member signature: parameters, returns, fields and
    /// properties, unwrapping generics so Task&lt;IClientSessionHandle&gt; is not missed.
    /// </summary>
    private static IEnumerable<Type> SignatureTypesOf(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(All))
        {
            foreach (var unwrapped in Unwrap(method.ReturnType)) yield return unwrapped;

            foreach (var parameter in method.GetParameters())
                foreach (var unwrapped in Unwrap(parameter.ParameterType)) yield return unwrapped;
        }

        foreach (var property in type.GetProperties(All))
            foreach (var unwrapped in Unwrap(property.PropertyType)) yield return unwrapped;

        foreach (var field in type.GetFields(All))
            foreach (var unwrapped in Unwrap(field.FieldType)) yield return unwrapped;
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (!type.IsGenericType) yield break;

        foreach (var argument in type.GetGenericArguments())
            foreach (var unwrapped in Unwrap(argument)) yield return unwrapped;
    }

    // ─── And the layer that is allowed to know ──────────────────────────────

    [Fact]
    public void InfrastructureIsTheLayerThatKnowsAboutMongo()
    {
        // The mirror image of the rule above. Without it, all the assertions here could be
        // satisfied by a project that simply does not talk to a database at all, and the
        // suite would pass while proving nothing.
        ReferencesOf(Infrastructure).Should().Contain(
            name => name.StartsWith("MongoDB", StringComparison.Ordinal),
            because: "these tests must fail if the driver moves somewhere else, not merely "
                   + "if it disappears");
    }
}
