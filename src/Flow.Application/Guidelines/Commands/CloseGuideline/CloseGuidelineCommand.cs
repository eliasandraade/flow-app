using MediatR;

namespace Flow.Application.Guidelines.Commands.CloseGuideline;

/// <summary>Ends a guideline by closing its validity period, preserving existing links.</summary>
public record CloseGuidelineCommand(Guid Id) : IRequest;
