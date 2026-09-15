using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Interfaces;
using Flow.Application.Common.Persistence;
using Flow.Domain.Entities;
using Flow.Domain.Enums;

namespace Flow.Application.Common.Authorization;

/// <summary>
/// Resource-level authorization for the read side of the innovation trail.
///
/// Role attributes on the controller answer "may this role use this endpoint at all".
/// They cannot answer "may this particular user read this particular document", and a
/// programme like Flow is full of endpoints where the second question is the one that
/// matters: knowing a GUID must never be enough to read someone else's idea, project or
/// financial result.
///
/// The rule, from the MVP specification's role matrix:
///
/// - <see cref="UserRole.Manager"/> and <see cref="UserRole.Leadership"/> read the whole
///   programme. Evaluating and steering it is what their roles exist for.
/// - <see cref="UserRole.Operator"/> reads only their own trail: their ideas, and the
///   projects and results that grew out of them. The specification says
///   "Operator (own)" for the project list, and this is what "own" means — the project
///   they were given ownership of, or the one created from an idea they submitted.
///
/// It lives in the Application layer, next to the handlers it protects, because it is a
/// use-case rule and not an HTTP concern. Duplicating it across handlers is how these
/// checks silently drift apart, which is exactly what happened before this existed.
/// </summary>
public sealed class ResourceAccessPolicy
{
    private readonly ICurrentUserService _currentUser;
    private readonly IIdeaRepository _ideas;

    public ResourceAccessPolicy(ICurrentUserService currentUser, IIdeaRepository ideas)
    {
        _currentUser = currentUser;
        _ideas = ideas;
    }

    /// <summary>True for the roles that are meant to see the whole programme.</summary>
    public bool HasProgrammeWideRead =>
        _currentUser.IsInRole(UserRole.Manager) || _currentUser.IsInRole(UserRole.Leadership);

    private Guid CurrentUserId =>
        _currentUser.UserId
        ?? throw new InvalidOperationException("Authenticated user identity could not be resolved.");

    /// <summary>
    /// An Operator may only open their own idea. Returns whether the caller owns it, which
    /// the detail DTO uses to decide what can be edited.
    /// </summary>
    public bool EnsureCanReadIdea(Idea idea)
    {
        ArgumentNullException.ThrowIfNull(idea);

        var isOwner = idea.SubmittedBy == _currentUser.UserId;

        if (!HasProgrammeWideRead && !isOwner)
            throw new ForbiddenException("You can only view your own ideas.");

        return isOwner;
    }

    /// <summary>
    /// Guards everything hanging off a project — the project itself, its timeline and its
    /// result — with one rule, so the financial figures cannot be reached through a door
    /// that someone forgot to lock.
    /// </summary>
    public async Task EnsureCanReadProjectAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (HasProgrammeWideRead) return;

        if (project.OwnerId == CurrentUserId) return;

        // The project may still be theirs by origin: it was created from their idea. One
        // targeted read rather than loading every idea the user ever submitted.
        if (project.SourceIdeaId is { } sourceIdeaId)
        {
            var sourceIdea = await _ideas.GetByIdAsync(sourceIdeaId, cancellationToken);
            if (sourceIdea?.SubmittedBy == CurrentUserId) return;
        }

        throw new ForbiddenException(
            "You can only view projects you own or that came from your own ideas.");
    }

    /// <summary>
    /// The list counterpart of <see cref="EnsureCanReadProjectAsync"/>. Returns null when
    /// the caller sees everything, and otherwise the scope the query must be narrowed to.
    ///
    /// Narrowing the query is not the same as filtering the results afterwards: paging and
    /// counts have to be computed over what the user may actually see, or page two starts
    /// leaking the existence of documents page one hid.
    /// </summary>
    public async Task<OperatorProjectScope?> ProjectListScopeAsync(CancellationToken cancellationToken)
    {
        if (HasProgrammeWideRead) return null;

        var ownIdeaIds = await _ideas.GetIdsSubmittedByAsync(CurrentUserId, cancellationToken);
        return new OperatorProjectScope(CurrentUserId, ownIdeaIds);
    }
}

/// <summary>
/// What a single Operator is allowed to see in the project list: projects they own, plus
/// projects created from one of their own ideas.
/// </summary>
public sealed record OperatorProjectScope(Guid OperatorId, IReadOnlyCollection<Guid> OwnIdeaIds);
