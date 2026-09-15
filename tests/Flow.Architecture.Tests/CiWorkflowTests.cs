using System.Text.RegularExpressions;
using FluentAssertions;

namespace Flow.Architecture.Tests;

/// <summary>
/// The promises the CI workflow makes, checked instead of assumed.
///
/// A continuous integration workflow is the one file whose bugs are invisible: when it is
/// wrong it does not fail, it simply does not run, and a green tick appears next to code
/// nothing looked at. That happened here — jobs opted out of pull request events to avoid a
/// duplicate run, and the effect was that an internal branch could reach master with no
/// validation at all, because the push trigger did not cover it either.
///
/// These live with the architecture guards rather than in a test project of their own for
/// the same reason those exist: they are invariants about the repository, not about a class.
/// The file is read as text on purpose. Adding a YAML parser would be a dependency to
/// maintain for a handful of assertions, and the properties that matter here — which
/// triggers exist, whether any job carries a condition — are visible in the text.
/// </summary>
public class CiWorkflowTests
{
    private static readonly string Workflow = ReadWorkflow();

    private static string ReadWorkflow()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Flow.sln")))
            directory = directory.Parent;

        directory.Should().NotBeNull(because: "the tests run from inside the repository");

        var path = Path.Combine(directory!.FullName, ".github", "workflows", "ci.yml");
        File.Exists(path).Should().BeTrue(because: $"the workflow is expected at {path}");

        return File.ReadAllText(path);
    }

    // ─── Every pull request is actually validated ───────────────────────────

    [Fact]
    public void NoJobOptsOutOfAnEvent()
    {
        // Job-level conditions sit at four spaces; a step's own `if:` is nested deeper
        // inside `steps:` and is none of this test's business. The distinction is the whole
        // point: a step may skip, a job may not, because a skipped job still reports success
        // and satisfies a branch protection rule without having run anything.
        var jobConditions = Regex.Matches(Workflow, @"^    if:.*$", RegexOptions.Multiline)
            .Select(match => match.Value.Trim())
            .ToArray();

        jobConditions.Should().BeEmpty(
            because: "a job that skips itself reports success without validating anything");
    }

    [Theory]
    [InlineData("push")]
    [InlineData("pull_request")]
    public void BothTriggersCoverTheProtectedBranches(string trigger)
    {
        // Pushes cover the branches we work on directly; pull requests cover everything
        // else, which includes a branch of this repository proposing a merge into one of
        // them. Dropping either leaves a way to reach master unchecked.
        var block = Regex.Match(
            Workflow,
            $@"^  {trigger}:\s*\n\s+branches:\s*\[(?<branches>[^\]]*)\]",
            RegexOptions.Multiline);

        block.Success.Should().BeTrue(because: $"{trigger} must be a trigger with a branch list");

        var branches = block.Groups["branches"].Value;
        branches.Should().Contain("master");
        branches.Should().Contain("sprint-2");
    }

    [Fact]
    public void ThePrivilegedPullRequestTriggerIsNotUsed()
    {
        // pull_request_target runs the base branch's workflow with a writable token and the
        // repository's secrets, for code the author of the pull request controls. It is the
        // documented way to hand a fork write access by accident.
        Workflow.Should().NotContain("pull_request_target");
    }

    // ─── A fork's pull request must be able to run all of this ──────────────

    [Fact]
    public void TheWorkflowAsksForNothingBeyondReadingTheCode()
    {
        Regex.IsMatch(Workflow, @"^permissions:\s*\n\s+contents: read\s*$", RegexOptions.Multiline)
            .Should().BeTrue(because: "nothing here publishes, comments or tags");

        // A pull request from a fork gets an empty secrets context. Any job that reached for
        // a secret would fail there, or worse, be tempted into pull_request_target.
        Workflow.Should().NotContain("secrets.");
    }

    // ─── Test results cannot collide ────────────────────────────────────────

    [Fact]
    public void EachTestProjectWritesAResultFileNamedAfterItself()
    {
        // LogFilePrefix appends a timestamp resolved to the second, so two projects that
        // finish within the same second write the same file and the totals below it silently
        // undercount. A name chosen per project cannot collide at all.
        // Matching the logger argument rather than the bare word: the step's comment says
        // why LogFilePrefix was rejected, and that explanation should not trip the check
        // that keeps it rejected.
        Workflow.Should().Contain("trx;LogFileName=",
            because: "result files must be named, not generated from a clock");

        Workflow.Should().NotContain("trx;LogFilePrefix",
            because: "a second-resolution timestamp is not a unique name");
    }

    [Fact]
    public void TheGuardStillRefusesASuiteThatDidNotRunInFull()
    {
        // The counters are the trx schema's, not the console summary's: a skipped test is
        // notExecuted there and there is no skipped attribute at all, so a guard summing the
        // wrong name would report a clean suite forever.
        Workflow.Should().Contain("notExecuted");
        Workflow.Should().Contain("$failed");
        Workflow.Should().Contain("MINIMUM_TESTS");
    }
}
