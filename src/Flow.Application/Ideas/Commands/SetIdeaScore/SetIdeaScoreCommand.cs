using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaScore;

/// <summary>Manager's direct 0..100 rating. Independent of Priority and of FlowScore.</summary>
public record SetIdeaScoreCommand(Guid IdeaId, int Score) : IRequest;
