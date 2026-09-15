using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Guidelines.Commands.UpdateGuideline;

public record UpdateGuidelineCommand(
    Guid Id,
    string Title,
    string Description,
    GuidelineCategory Category,
    string? Campaign,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil) : IRequest;
