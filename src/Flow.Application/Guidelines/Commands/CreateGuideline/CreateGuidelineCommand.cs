using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Commands.CreateGuideline;

public record CreateGuidelineCommand(
    string Title,
    string Description,
    GuidelineCategory Category,
    string? Campaign,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil) : IRequest<GuidelineDto>;
