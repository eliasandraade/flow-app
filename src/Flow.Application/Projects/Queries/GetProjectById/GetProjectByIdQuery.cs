using MediatR;

namespace Flow.Application.Projects.Queries.GetProjectById;

public record GetProjectByIdQuery(Guid ProjectId) : IRequest<ProjectDetailDto>;
