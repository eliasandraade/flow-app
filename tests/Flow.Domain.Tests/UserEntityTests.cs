using Flow.Domain.Entities;
using Flow.Domain.Enums;
using FluentAssertions;

namespace Flow.Domain.Tests;

public class UserEntityTests
{
    private static User NewUser(UserRole role = UserRole.Operator) =>
        User.Create("Carla Souza", "carla@flow.demo", role);

    [Fact]
    public void Create_NormalisesEmailAndSeedsIdentityFields()
    {
        var user = User.Create("Carla Souza", "Carla@Flow.Demo", UserRole.Operator);

        user.Email.Should().Be("Carla@Flow.Demo");
        user.UserName.Should().Be("Carla@Flow.Demo");
        user.NormalizedEmail.Should().Be("CARLA@FLOW.DEMO");
        user.NormalizedUserName.Should().Be("CARLA@FLOW.DEMO");
        user.SecurityStamp.Should().NotBeNullOrWhiteSpace();
        user.Points.Should().Be(0);
        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public void AddPoints_Accumulates()
    {
        var user = NewUser();

        user.AddPoints(50);
        user.AddPoints(30);

        user.Points.Should().Be(80);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AddPoints_RejectsNonPositiveAwards(int points)
    {
        var user = NewUser();

        var act = () => user.AddPoints(points);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRole_IsIdempotentAndCaseInsensitive()
    {
        var user = NewUser();

        user.AddRole("Manager");
        user.AddRole("Manager");
        user.AddRole("MANAGER");

        user.Roles.Should().ContainSingle().Which.Should().Be("Manager");
    }

    [Fact]
    public void HasRole_IgnoresCasing()
    {
        var user = NewUser();
        user.AddRole("Leadership");

        // UserManager passes the normalised name down to the store, so this has to match.
        user.HasRole("LEADERSHIP").Should().BeTrue();
        user.HasRole("Leadership").Should().BeTrue();
        user.HasRole("Manager").Should().BeFalse();
    }

    [Fact]
    public void RemoveRole_IgnoresCasing()
    {
        var user = NewUser();
        user.AddRole("Manager");

        user.RemoveRole("MANAGER");

        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public void SetRole_ChangesTheBusinessRole()
    {
        var user = NewUser();

        user.SetRole(UserRole.Leadership);

        user.Role.Should().Be(UserRole.Leadership);
    }
}
