using Flow.Domain.Exceptions;

namespace Flow.Domain.Entities;

public class IdeaComment
{
    public Guid Id { get; private set; }
    public Guid IdeaId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private IdeaComment() { }

    public static IdeaComment Create(Guid ideaId, Guid authorId, string authorName, string body)
    {
        if (ideaId == Guid.Empty) throw new DomainException("IdeaComment must reference a valid idea.");
        if (authorId == Guid.Empty) throw new DomainException("IdeaComment must reference a valid author.");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Comment body cannot be empty.");

        return new IdeaComment
        {
            Id = Guid.NewGuid(),
            IdeaId = ideaId,
            AuthorId = authorId,
            AuthorName = authorName,
            Body = body.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
