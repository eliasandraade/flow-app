using MediatR;

namespace Flow.Application.Guidelines.Commands.DeleteGuideline;

public record DeleteGuidelineCommand(Guid Id) : IRequest;
