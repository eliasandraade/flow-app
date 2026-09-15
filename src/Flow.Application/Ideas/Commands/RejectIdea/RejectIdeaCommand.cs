using MediatR;

namespace Flow.Application.Ideas.Commands.RejectIdea;

public record RejectIdeaCommand(Guid IdeaId, string ManagerComment) : IRequest;
