using Flow.Application.Common.Exceptions;
using Flow.Application.Common.Persistence;
using MediatR;

namespace Flow.Application.Guidelines.Queries.GetGuidelineById;

public class GetGuidelineByIdQueryHandler : IRequestHandler<GetGuidelineByIdQuery, GuidelineDto>
{
    private readonly IGuidelineRepository _guidelines;

    public GetGuidelineByIdQueryHandler(IGuidelineRepository guidelines) => _guidelines = guidelines;

    public async Task<GuidelineDto> Handle(
        GetGuidelineByIdQuery request, CancellationToken cancellationToken)
    {
        var guideline = await _guidelines.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Guideline", request.Id);

        return GuidelineDto.From(guideline, DateTimeOffset.UtcNow);
    }
}
