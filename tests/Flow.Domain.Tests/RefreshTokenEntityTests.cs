using Flow.Domain.Entities;
using Flow.Domain.Exceptions;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class RefreshTokenEntityTests
{
    private static RefreshToken Issue(string raw = "raw-token-value", int daysValid = 7) =>
        RefreshToken.Issue(Guid.NewGuid(), raw, DateTimeOffset.UtcNow.AddDays(daysValid));

    [Fact]
    public void Issue_NeverStoresTheRawTokenValue()
    {
        const string raw = "a-very-secret-refresh-token";

        var token = Issue(raw);

        token.TokenHash.Should().NotBe(raw);
        token.TokenHash.Should().NotContain(raw);
        token.TokenHash.Should().HaveLength(64, because: "SHA-256 hex is 64 characters");
    }

    [Fact]
    public void Hash_IsStableSoLookupByTokenWorks()
    {
        const string raw = "same-token";

        RefreshToken.Hash(raw).Should().Be(RefreshToken.Hash(raw));
    }

    [Fact]
    public void Hash_DiffersBetweenTokens()
    {
        RefreshToken.Hash("token-a").Should().NotBe(RefreshToken.Hash("token-b"));
    }

    [Fact]
    public void Issue_WithoutAUser_IsRejected()
    {
        var act = () => RefreshToken.Issue(Guid.Empty, "raw", DateTimeOffset.UtcNow.AddDays(1));

        act.Should().Throw<DomainException>().WithMessage("*valid user*");
    }

    [Fact]
    public void NewToken_IsActive()
    {
        Issue().IsActive.Should().BeTrue();
    }

    [Fact]
    public void ExpiredToken_IsNotActive()
    {
        Issue(daysValid: -1).IsActive.Should().BeFalse();
    }

    [Fact]
    public void RevokedToken_IsNotActive()
    {
        var token = Issue();

        token.Revoke();

        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_IsIdempotentAndKeepsTheOriginalMoment()
    {
        var token = Issue();
        token.Revoke();
        var firstRevocation = token.RevokedAt;

        token.Revoke();

        token.RevokedAt.Should().Be(firstRevocation);
    }

    [Fact]
    public void RotateTo_RevokesTheOldTokenAndLinksItsReplacement()
    {
        var oldToken = Issue("old");
        var newToken = Issue("new");

        oldToken.RotateTo(newToken);

        oldToken.IsRevoked.Should().BeTrue();
        oldToken.ReplacedByTokenHash.Should().Be(newToken.TokenHash);
        newToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_EvaluatesAgainstTheGivenMoment()
    {
        var token = Issue(daysValid: 7);

        token.IsActiveAt(DateTimeOffset.UtcNow.AddDays(3)).Should().BeTrue();
        token.IsActiveAt(DateTimeOffset.UtcNow.AddDays(8)).Should().BeFalse();
    }
}
