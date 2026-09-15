using MediatR;

namespace Flow.Application.Ideas.Commands.SetIdeaFlowScore;

/// <summary>
/// Records the FlowScore components for an idea. The score itself is always recomputed
/// from these components by the domain, so a caller cannot inject an arbitrary total.
/// </summary>
public record SetIdeaFlowScoreCommand(
    Guid IdeaId,
    int? StrategicAlignment,
    int Impact,
    int Feasibility,
    int Urgency,
    int Confidence) : IRequest<FlowScoreDto>;
