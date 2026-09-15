using System.Text.Json;
using Flow.Application.Dashboard;
using Flow.Application.Dashboard.Queries.GetDashboardSummary;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Assistant.Commands.GenerateExecutiveInsights;

public record GenerateExecutiveInsightsCommand : IRequest<ExecutiveInsightDto>;

public record ExecutiveInsightDto(
    Guid AssistantRunId,
    string Model,
    long LatencyMs,
    ExecutiveInsight Insight);

/// <summary>
/// Builds the executive narrative on top of the dashboard the API already computes.
///
/// The model receives exactly the figures leadership sees on screen and nothing else, so
/// an insight can never cite a number the dashboard does not show.
/// </summary>
public class GenerateExecutiveInsightsCommandHandler
    : IRequestHandler<GenerateExecutiveInsightsCommand, ExecutiveInsightDto>
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly IMediator _mediator;
    private readonly IExecutiveInsightService _insights;
    private readonly AssistantRunRecorder _recorder;

    public GenerateExecutiveInsightsCommandHandler(
        IMediator mediator,
        IExecutiveInsightService insights,
        AssistantRunRecorder recorder)
    {
        _mediator = mediator;
        _insights = insights;
        _recorder = recorder;
    }

    public async Task<ExecutiveInsightDto> Handle(
        GenerateExecutiveInsightsCommand request, CancellationToken cancellationToken)
    {
        var summary = await _mediator.Send(new GetDashboardSummaryQuery(), cancellationToken);

        var context = new ExecutiveInsightContext(
            DashboardJson: JsonSerializer.Serialize(Trim(summary), PayloadOptions),
            GeneratedAt: summary.GeneratedAt);

        var result = await _insights.GenerateAsync(context, cancellationToken);

        var run = await _recorder.RecordAsync(
            AssistantOperation.ExecutiveInsights, result, cancellationToken);

        if (!result.IsSuccess) throw AssistantRunRecorder.ToException(result);

        return new ExecutiveInsightDto(run.Id, result.Model, result.LatencyMs, result.Value!);
    }

    /// <summary>
    /// Sends the aggregates and the short ranked lists, but not the full risk and blocker
    /// listings, which can grow without bound. Everything needed for a conclusion is here;
    /// what is dropped is length, not signal.
    /// </summary>
    private static object Trim(DashboardSummaryDto s) => new
    {
        s.Ideas,
        s.Projects,
        s.Financial,
        s.Impact,
        BlockedProjects = s.BlockedProjects.Take(10),
        ProjectsAtRisk = s.ProjectsAtRisk.Take(10),
        s.TopIdeas,
        s.TopProjects,
        s.ByStrategy,
        s.ByCampaign,
        s.Trends,
        s.GeneratedAt
    };
}
