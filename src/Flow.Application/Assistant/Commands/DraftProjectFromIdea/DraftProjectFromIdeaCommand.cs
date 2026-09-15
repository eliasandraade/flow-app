using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Domain.Enums;
using Flow.Domain.Exceptions;
using MediatR;

namespace Flow.Application.Assistant.Commands.DraftProjectFromIdea;

/// <summary>
/// Produces a project proposal from an approved idea.
///
/// The result is a draft and nothing more. No project is created here, no state changes,
/// nothing is written to the domain. The manager reviews the preview, edits it, and the
/// project only comes into existence through the ordinary conversion command — with
/// authorisation, validation, domain rules, a snapshot and an audit entry.
/// </summary>
public record DraftProjectFromIdeaCommand(Guid IdeaId) : IRequest<ProjectDraftDto>;

public record ProjectDraftDto(
    Guid AssistantRunId,
    Guid IdeaId,
    string Model,
    long LatencyMs,
    ProjectDraft Draft);

public class DraftProjectFromIdeaCommandHandler
    : IRequestHandler<DraftProjectFromIdeaCommand, ProjectDraftDto>
{
    private const int MaxComparableOutcomes = 5;

    private readonly IInnovationAssistant _assistant;
    private readonly IIdeaRepository _ideas;
    private readonly IIdeaCommentRepository _comments;
    private readonly IGuidelineRepository _guidelines;
    private readonly IProjectRepository _projects;
    private readonly IResultRepository _results;
    private readonly AssistantRunRecorder _recorder;

    public DraftProjectFromIdeaCommandHandler(
        IInnovationAssistant assistant,
        IIdeaRepository ideas,
        IIdeaCommentRepository comments,
        IGuidelineRepository guidelines,
        IProjectRepository projects,
        IResultRepository results,
        AssistantRunRecorder recorder)
    {
        _assistant = assistant;
        _ideas = ideas;
        _comments = comments;
        _guidelines = guidelines;
        _projects = projects;
        _results = results;
        _recorder = recorder;
    }

    public async Task<ProjectDraftDto> Handle(
        DraftProjectFromIdeaCommand request, CancellationToken cancellationToken)
    {
        var idea = await _ideas.GetByIdAsync(request.IdeaId, cancellationToken)
            ?? throw new NotFoundException("Idea", request.IdeaId);

        if (idea.Status != IdeaStatus.Approved)
            throw new DomainException("Only approved ideas can be drafted into projects.");

        var now = DateTimeOffset.UtcNow;

        var guidelines = await _guidelines.QueryAsync(
            new GuidelineFilter { CurrentAt = now }, cancellationToken);

        var guideline = idea.LinkedGuidelineId is { } id
            ? await _guidelines.GetByIdAsync(id, cancellationToken)
            : null;

        var comments = await _comments.GetForIdeaAsync(idea.Id, cancellationToken);

        var assistantIdea = new AssistantIdea(
            idea.Id, idea.Title, idea.Problem, idea.Description,
            idea.Status.ToString(), idea.Priority.ToString(),
            idea.Score, idea.FlowScore?.Total,
            idea.FlowScore is null ? null : new Dictionary<string, int>
            {
                ["strategicAlignment"] = idea.FlowScore.Components.StrategicAlignment,
                ["impact"] = idea.FlowScore.Components.Impact,
                ["feasibility"] = idea.FlowScore.Components.Feasibility,
                ["urgency"] = idea.FlowScore.Components.Urgency,
                ["confidence"] = idea.FlowScore.Components.Confidence
            },
            guideline?.Title,
            guideline?.IsCurrentAt(now) ?? false,
            comments.Count,
            0);

        var context = new ProjectDraftContext(
            assistantIdea,
            guidelines
                .Select(g => new AssistantGuideline(
                    g.Id, g.Title, g.Category.ToString(), g.Campaign, g.IsCurrentAt(now)))
                .ToList(),
            await GetComparableOutcomesAsync(idea.LinkedGuidelineId, cancellationToken));

        var result = await _assistant.DraftProjectAsync(context, cancellationToken);

        var run = await _recorder.RecordAsync(
            AssistantOperation.DraftProject, result, cancellationToken);

        if (!result.IsSuccess) throw AssistantRunRecorder.ToException(result);

        return new ProjectDraftDto(run.Id, idea.Id, result.Model, result.LatencyMs, result.Value!);
    }

    /// <summary>
    /// Real outcomes of comparable finished projects, so duration and cost suggestions are
    /// anchored in what this organisation actually achieved rather than invented.
    /// </summary>
    private async Task<IReadOnlyList<HistoricalProjectOutcome>> GetComparableOutcomesAsync(
        Guid? guidelineId, CancellationToken cancellationToken)
    {
        var completed = await _projects.QueryAsync(new ProjectFilter
        {
            Status = ProjectStatus.Completed,
            LinkedGuidelineId = guidelineId,
            Take = MaxComparableOutcomes
        }, cancellationToken);

        // Fall back to any completed project when the strategy has no history of its own.
        if (completed.Count == 0 && guidelineId is not null)
        {
            completed = await _projects.QueryAsync(new ProjectFilter
            {
                Status = ProjectStatus.Completed,
                Take = MaxComparableOutcomes
            }, cancellationToken);
        }

        var outcomes = new List<HistoricalProjectOutcome>(completed.Count);

        foreach (var project in completed)
        {
            var result = await _results.GetByProjectIdAsync(project.Id, cancellationToken);

            var duration = project.StartDate is not null && project.CompletedAt is not null
                ? (int)(project.CompletedAt.Value - project.StartDate.Value).TotalDays
                : 0;

            var net = result?.Actual is null
                ? null
                : (decimal?)((result.Actual.Revenue ?? 0m)
                    + (result.Actual.Savings ?? 0m)
                    - (result.Actual.Cost ?? 0m));

            outcomes.Add(new HistoricalProjectOutcome(
                project.Title, project.Status.ToString(), duration, net, result?.Actual?.Roi));
        }

        return outcomes;
    }
}
