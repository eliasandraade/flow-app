using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Ideas.Queries.GetIdeas;

public record GetIdeasQuery(
    Guid? SubmittedById = null,
    IdeaStatus? Status = null,
    IdeaPriority? Priority = null,
    Guid? LinkedGuidelineId = null,
    int? MinScore = null,
    string? SortBy = null,
    int Skip = 0,
    int Take = 50) : IRequest<IReadOnlyList<IdeaSummaryDto>>;
