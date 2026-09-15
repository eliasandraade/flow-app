using MediatR;

namespace Flow.Application.Guidelines.Queries.GetCurrentGuidelines;

public record GetCurrentGuidelinesQuery : IRequest<IReadOnlyList<GuidelineDto>>;
