using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Projects.Commands.StartProject;

public class StartProjectCommandHandler : IRequestHandler<StartProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly ProjectTransitionRecorder _recorder;

    public StartProjectCommandHandler(IProjectRepository projects, ProjectTransitionRecorder recorder)
    {
        _projects = projects;
        _recorder = recorder;
    }

    public async Task Handle(StartProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var previous = project.Status.ToString();
        project.Start();

        await _recorder.RecordAsync(
            project, ProjectActions.Started, previousValue: previous, cancellationToken: cancellationToken);
    }
}
