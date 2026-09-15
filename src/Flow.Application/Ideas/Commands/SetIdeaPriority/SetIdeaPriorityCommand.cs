using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaPriority;

public record SetIdeaPriorityCommand(Guid IdeaId, IdeaPriority Priority) : IRequest;
