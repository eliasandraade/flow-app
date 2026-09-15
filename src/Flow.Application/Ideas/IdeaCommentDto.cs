using Flow.Domain.Entities;

namespace Flow.Application.Ideas;

public record IdeaCommentDto(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAt)
{
    public static IdeaCommentDto From(IdeaComment c) =>
        new(c.Id, c.AuthorId, c.AuthorName, c.Body, c.CreatedAt);
}
