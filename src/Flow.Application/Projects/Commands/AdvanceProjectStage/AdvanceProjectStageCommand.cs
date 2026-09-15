using Flow.Domain.Enums;
using MediatR;

namespace Flow.Application.Projects.Commands.AdvanceProjectStage;

public record AdvanceProjectStageCommand(Guid ProjectId, ProjectStage Stage) : IRequest;
