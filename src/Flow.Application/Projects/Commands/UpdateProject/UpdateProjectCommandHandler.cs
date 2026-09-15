using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Projects.Commands.UpdateProject;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ProjectTransitionRecorder _recorder;

    public UpdateProjectCommandHandler(
        IProjectRepository projects,
        IUserRepository users,
        ProjectTransitionRecorder recorder)
    {
        _projects = projects;
        _users = users;
        _recorder = recorder;
    }

    public async Task Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var owner = await _users.GetByIdAsync(request.OwnerId, cancellationToken)
            ?? throw new NotFoundException("User", request.OwnerId);

        var previousTitle = project.Title;

        project.Update(
            request.Title, request.Description, request.Priority,
            owner.Id, owner.Name,
            request.EstimatedCost, request.ActualCost, request.Deadline);

        await _recorder.RecordAsync(
            project, ProjectActions.Updated,
            previousValue: previousTitle,
            newValue: project.Title,
            cancellationToken: cancellationToken);
    }
}
