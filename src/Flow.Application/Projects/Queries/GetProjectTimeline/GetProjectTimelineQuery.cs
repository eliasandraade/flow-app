using MediatR;

namespace Flow.Application.Projects.Queries.GetProjectTimeline;

public record GetProjectTimelineQuery(Guid ProjectId) : IRequest<IReadOnlyList<TimelineEntryDto>>;
