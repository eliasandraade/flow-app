using Flow.Application.Guidelines;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelineById;

public record GetGuidelineByIdQuery(Guid Id) : IRequest<GuidelineDto>;
