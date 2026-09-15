using MediatR;

namespace Flow.Application.Results.Commands.RecordResult;

public record RecordResultCommand(
    Guid ProjectId,
    decimal? EstimatedRevenue,
    decimal? EstimatedSavings,
    decimal? EstimatedCost,
    decimal? ActualRevenue,
    decimal? ActualSavings,
    decimal? ActualCost,
    int? PaybackPeriodMonths,
    decimal? ProductivityGainPercent,
    decimal? TimeSavedHours,
    decimal? QualityGainPercent,
    string? Notes) : IRequest<ResultDto>;
