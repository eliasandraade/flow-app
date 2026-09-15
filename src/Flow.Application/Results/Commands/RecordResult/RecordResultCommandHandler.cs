using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using Flow.Application.Common.Services;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Results.Commands.RecordResult;

public class RecordResultCommandHandler : IRequestHandler<RecordResultCommand, ResultDto>
{
    private readonly IProjectRepository _projects;
    private readonly IResultRepository _results;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditTrail _audit;
    private readonly NotificationPublisher _notifications;

    public RecordResultCommandHandler(
        IProjectRepository projects,
        IResultRepository results,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        AuditTrail audit,
        NotificationPublisher notifications)
    {
        _projects = projects;
        _results = results;
        _users = users;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<ResultDto> Handle(RecordResultCommand request, CancellationToken cancellationToken)
    {
        var actorId = _audit.ActorId;

        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var result = await _results.GetByProjectIdAsync(request.ProjectId, cancellationToken);

        var hasEstimated = request.EstimatedRevenue.HasValue
            || request.EstimatedSavings.HasValue
            || request.EstimatedCost.HasValue;

        var hasActual = request.ActualRevenue.HasValue
            || request.ActualSavings.HasValue
            || request.ActualCost.HasValue;

        var hasImpact = request.ProductivityGainPercent.HasValue
            || request.TimeSavedHours.HasValue
            || request.QualityGainPercent.HasValue;

        var hasNotes = request.PaybackPeriodMonths.HasValue || request.Notes is not null;

        if (!hasEstimated && !hasActual && !hasImpact && !hasNotes)
        {
            if (result is not null) return ResultDto.From(result);

            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["fields"] = ["At least one field must be provided to record a result."]
            });
        }

        result ??= Result.Create(request.ProjectId, actorId);

        // Estimated and actual are written through separate operations so a planning figure
        // can never be silently promoted into a realised one.
        if (hasEstimated)
            result.SetEstimated(request.EstimatedRevenue, request.EstimatedSavings, request.EstimatedCost);

        if (hasActual)
            result.SetActual(request.ActualRevenue, request.ActualSavings, request.ActualCost);

        if (hasImpact)
            result.SetImpactMetrics(
                request.ProductivityGainPercent, request.TimeSavedHours, request.QualityGainPercent);

        if (hasNotes)
            result.SetNotes(request.PaybackPeriodMonths, request.Notes);

        var leadership = hasActual
            ? await _users.GetByRoleAsync(UserRole.Leadership, cancellationToken)
            : [];

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _results.UpsertAsync(result, ct);

            await _audit.RecordAsync(
                nameof(Project), project.Id, "ResultRecorded",
                newValue: hasActual ? "Actual" : "Estimated", cancellationToken: ct);

            // Only a realised result is worth interrupting leadership for; an estimate is
            // planning noise at that level.
            if (hasActual)
            {
                await _notifications.PublishManyAsync(
                    leadership.Select(l => l.Id),
                    NotificationType.ResultRecorded,
                    "Resultado realizado registrado",
                    $"\"{project.Title}\" teve resultados reais registrados.",
                    $"flow://projects/{project.Id}/result",
                    leaderId => $"ResultRecorded:{project.Id}:{result.UpdatedAt.Ticks}:{leaderId}",
                    ct);
            }
        }, cancellationToken);

        return ResultDto.From(result);
    }
}
