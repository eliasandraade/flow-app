using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Dashboard.Queries.GetProjectDashboard;

public class GetProjectDashboardQueryHandler
    : IRequestHandler<GetProjectDashboardQuery, ProjectDashboardDto>
{
    private readonly IProjectRepository _projects;
    private readonly IIdeaRepository _ideas;
    private readonly IGuidelineRepository _guidelines;
    private readonly IResultRepository _results;
    private readonly IDashboardReadRepository _dashboard;

    public GetProjectDashboardQueryHandler(
        IProjectRepository projects,
        IIdeaRepository ideas,
        IGuidelineRepository guidelines,
        IResultRepository results,
        IDashboardReadRepository dashboard)
    {
        _projects = projects;
        _ideas = ideas;
        _guidelines = guidelines;
        _results = results;
        _dashboard = dashboard;
    }

    public async Task<ProjectDashboardDto> Handle(
        GetProjectDashboardQuery request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var now = DateTimeOffset.UtcNow;
        var aggregates = await _dashboard.GetProjectDetailAggregatesAsync(project.Id, cancellationToken);
        var result = await _results.GetByProjectIdAsync(project.Id, cancellationToken);

        string? sourceIdeaTitle = null;
        if (project.SourceIdeaId is { } ideaId)
            sourceIdeaTitle = (await _ideas.GetByIdAsync(ideaId, cancellationToken))?.Title;

        string? guidelineTitle = null;
        if (project.LinkedGuidelineId is { } guidelineId)
            guidelineTitle = (await _guidelines.GetByIdAsync(guidelineId, cancellationToken))?.Title;

        ResultSummaryDto? resultSummary = null;
        if (result is not null)
        {
            resultSummary = new ResultSummaryDto(
                EstimatedNetValue: NetValue(result.Estimated),
                EstimatedRoi: result.Estimated?.Roi,
                ActualNetValue: NetValue(result.Actual),
                ActualRoi: result.Actual?.Roi,
                PaybackPeriodMonths: result.PaybackPeriodMonths,
                ProductivityGainPercent: result.ProductivityGainPercent,
                TimeSavedHours: result.TimeSavedHours,
                QualityGainPercent: result.QualityGainPercent);
        }

        return new ProjectDashboardDto(
            project.Id, project.Title, project.Status.ToString(), project.Stage.ToString(),
            project.ProgressPercentage, project.OwnerId, project.OwnerName,
            project.SourceIdeaId, sourceIdeaTitle,
            project.LinkedGuidelineId, guidelineTitle,
            project.Deadline,
            project.Deadline is null ? null : (int)Math.Floor((project.Deadline.Value - now).TotalDays),
            project.IsOverdueAt(now),
            project.IsAtRiskAt(now),
            aggregates.TimesBlocked,
            Math.Round(aggregates.TotalDaysBlocked, 1),
            aggregates.SnapshotCount,
            aggregates.AuditEntryCount,
            resultSummary,
            now);
    }

    private static decimal? NetValue(Domain.ValueObjects.ResultMeasurement? m) => m is null
        ? null
        : (m.Revenue ?? 0m) + (m.Savings ?? 0m) - (m.Cost ?? 0m);
}
