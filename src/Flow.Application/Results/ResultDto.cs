using Flow.Domain.Entities;

namespace Flow.Application.Results;

public record ResultMeasurementDto(
    decimal? Revenue,
    decimal? Savings,
    decimal? Cost,
    decimal? Roi,
    decimal? NetValue,
    DateTimeOffset RecordedAt)
{
    public static ResultMeasurementDto? From(Domain.ValueObjects.ResultMeasurement? m) => m is null
        ? null
        : new ResultMeasurementDto(
            m.Revenue,
            m.Savings,
            m.Cost,
            m.Roi,
            NetValue: (m.Revenue ?? 0m) + (m.Savings ?? 0m) - (m.Cost ?? 0m),
            m.RecordedAt);
}

public record ResultDto(
    Guid Id,
    Guid ProjectId,
    ResultMeasurementDto? Estimated,
    ResultMeasurementDto? Actual,
    int? PaybackPeriodMonths,
    decimal? ProductivityGainPercent,
    decimal? TimeSavedHours,
    decimal? QualityGainPercent,
    string? Notes,
    Guid RecordedBy,
    DateTimeOffset RecordedAt,
    DateTimeOffset UpdatedAt)
{
    public static ResultDto From(Result r) => new(
        r.Id,
        r.ProjectId,
        ResultMeasurementDto.From(r.Estimated),
        ResultMeasurementDto.From(r.Actual),
        r.PaybackPeriodMonths,
        r.ProductivityGainPercent,
        r.TimeSavedHours,
        r.QualityGainPercent,
        r.Notes,
        r.RecordedBy,
        r.CreatedAt,
        r.UpdatedAt);
}
