using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Assistant.Commands.CompareIdeasWithAssistant;

/// <summary>
/// Asks the copilot to compare ideas the manager is reviewing.
///
/// Modelled as a command rather than a query because it has a real, metered side effect
/// at the provider and produces a governance record.
/// </summary>
public record CompareIdeasWithAssistantCommand(
    IReadOnlyList<Guid> IdeaIds,
    string? Question) : IRequest<AssistantComparisonDto>;

public record AssistantComparisonDto(
    Guid AssistantRunId,
    string Model,
    long LatencyMs,
    IdeaComparisonInsight Insight);

public class CompareIdeasWithAssistantCommandHandler
    : IRequestHandler<CompareIdeasWithAssistantCommand, AssistantComparisonDto>
{
    private const int MaxIdeas = 5;

    private readonly IInnovationAssistant _assistant;
    private readonly IIdeaRepository _ideas;
    private readonly IIdeaCommentRepository _comments;
    private readonly IGuidelineRepository _guidelines;
    private readonly AssistantRunRecorder _recorder;

    public CompareIdeasWithAssistantCommandHandler(
        IInnovationAssistant assistant,
        IIdeaRepository ideas,
        IIdeaCommentRepository comments,
        IGuidelineRepository guidelines,
        AssistantRunRecorder recorder)
    {
        _assistant = assistant;
        _ideas = ideas;
        _comments = comments;
        _guidelines = guidelines;
        _recorder = recorder;
    }

    public async Task<AssistantComparisonDto> Handle(
        CompareIdeasWithAssistantCommand request, CancellationToken cancellationToken)
    {
        var ids = request.IdeaIds.Distinct().ToList();

        if (ids.Count is < 2 or > MaxIdeas)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ideaIds"] = [$"Between 2 and {MaxIdeas} ideas are required for a comparison."]
            });

        var ideas = await _ideas.GetByIdsAsync(ids, cancellationToken);

        var missing = ids.Except(ideas.Select(i => i.Id)).ToList();
        if (missing.Count > 0) throw new NotFoundException("Idea", missing[0]);

        var now = DateTimeOffset.UtcNow;

        var guidelines = await _guidelines.QueryAsync(
            new GuidelineFilter { CurrentAt = now }, cancellationToken);

        var guidelineById = guidelines.ToDictionary(g => g.Id);

        var context = new IdeaComparisonContext(
            Ideas: await Task.WhenAll(ideas.Select(i => ToAssistantIdeaAsync(i, guidelineById, now, cancellationToken))),
            CurrentGuidelines: guidelines
                .Select(g => new AssistantGuideline(
                    g.Id, g.Title, g.Category.ToString(), g.Campaign, g.IsCurrentAt(now)))
                .ToList(),
            Question: request.Question);

        var result = await _assistant.CompareIdeasAsync(context, cancellationToken);

        // The run is recorded whether the call succeeded or not: a failed assistant call is
        // part of the operational picture too.
        var run = await _recorder.RecordAsync(
            AssistantOperation.CompareIdeas, result, cancellationToken);

        if (!result.IsSuccess) throw AssistantRunRecorder.ToException(result);

        return new AssistantComparisonDto(run.Id, result.Model, result.LatencyMs, result.Value!);
    }

    private async Task<AssistantIdea> ToAssistantIdeaAsync(
        Idea idea,
        IReadOnlyDictionary<Guid, StrategicGuideline> guidelines,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        StrategicGuideline? guideline = null;
        if (idea.LinkedGuidelineId is { } id) guidelines.TryGetValue(id, out guideline);

        var comments = await _comments.GetForIdeaAsync(idea.Id, cancellationToken);

        return new AssistantIdea(
            idea.Id,
            idea.Title,
            idea.Problem,
            idea.Description,
            idea.Status.ToString(),
            idea.Priority.ToString(),
            idea.Score,
            idea.FlowScore?.Total,
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
            idea.Status == IdeaStatus.UnderReview ? (int)(now - idea.UpdatedAt).TotalDays : 0);
    }
}
