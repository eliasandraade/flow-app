using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelines;

public record GetGuidelinesQuery(
    GuidelineCategory? Category = null,
    string? Campaign = null,
    bool CurrentOnly = false) : IRequest<IReadOnlyList<GuidelineDto>>;
