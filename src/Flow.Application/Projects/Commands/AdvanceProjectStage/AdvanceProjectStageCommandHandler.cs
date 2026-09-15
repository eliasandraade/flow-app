using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Projects.Commands.AdvanceProjectStage;

public class AdvanceProjectStageCommandHandler : IRequestHandler<AdvanceProjectStageCommand>
{
    private readonly IProjectRepository _projects;
    private readonly ProjectTransitionRecorder _recorder;

    public AdvanceProjectStageCommandHandler(
        IProjectRepository projects, ProjectTransitionRecorder recorder)
    {
        _projects = projects;
        _recorder = recorder;
    }

    public async Task Handle(AdvanceProjectStageCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var previous = project.Stage.ToString();
        project.AdvanceStage(request.Stage);

        await _recorder.RecordAsync(
            project, ProjectActions.StageChanged,
            previousValue: previous,
            newValue: project.Stage.ToString(),
            cancellationToken: cancellationToken);
    }
}
