using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Queries.GetProjects;

public record GetProjectsQuery(
    Guid? OwnerId = null,
    ProjectStatus? Status = null,
    ProjectStage? Stage = null,
    Guid? LinkedGuidelineId = null,
    int Skip = 0,
    int Take = 50) : IRequest<IReadOnlyList<ProjectSummaryDto>>;
