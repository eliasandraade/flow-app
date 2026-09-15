using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeaById;

public record GetIdeaByIdQuery(Guid IdeaId) : IRequest<IdeaDetailDto>;
