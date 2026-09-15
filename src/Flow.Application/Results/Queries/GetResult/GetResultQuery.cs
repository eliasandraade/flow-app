using MediatR;

namespace Flow.Application.Results.Queries.GetResult;

public record GetResultQuery(Guid ProjectId) : IRequest<ResultDto>;
