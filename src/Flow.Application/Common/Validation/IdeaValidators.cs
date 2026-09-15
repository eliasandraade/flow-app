using FluentValidation;
using Flow.Application.Ideas.Commands.AddIdeaComment;
using Flow.Application.Ideas.Commands.CreateIdea;
using Flow.Application.Ideas.Commands.RejectIdea;
using Flow.Application.Ideas.Commands.SetIdeaFlowScore;
using Flow.Application.Ideas.Commands.SetIdeaScore;
using Flow.Application.Ideas.Commands.UpdateIdea;

namespace Flow.Application.Common.Validation;

public sealed class CreateIdeaCommandValidator : AbstractValidator<CreateIdeaCommand>
{
    public CreateIdeaCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Problem).NotEmpty().MaximumLength(4000);
    }
}

public sealed class UpdateIdeaCommandValidator : AbstractValidator<UpdateIdeaCommand>
{
    public UpdateIdeaCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Problem).NotEmpty().MaximumLength(4000);
    }
}

public sealed class RejectIdeaCommandValidator : AbstractValidator<RejectIdeaCommand>
{
    public RejectIdeaCommandValidator() =>
        RuleFor(x => x.ManagerComment)
            .NotEmpty().WithMessage("Informe o motivo da recusa.")
            .MaximumLength(2000);
}

public sealed class AddIdeaCommentCommandValidator : AbstractValidator<AddIdeaCommentCommand>
{
    public AddIdeaCommentCommandValidator() =>
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
}

public sealed class SetIdeaScoreCommandValidator : AbstractValidator<SetIdeaScoreCommand>
{
    public SetIdeaScoreCommandValidator() =>
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
}

public sealed class SetIdeaFlowScoreCommandValidator : AbstractValidator<SetIdeaFlowScoreCommand>
{
    public SetIdeaFlowScoreCommandValidator()
    {
        RuleFor(x => x.StrategicAlignment).InclusiveBetween(0, 10).When(x => x.StrategicAlignment.HasValue);
        RuleFor(x => x.Impact).InclusiveBetween(0, 10);
        RuleFor(x => x.Feasibility).InclusiveBetween(0, 10);
        RuleFor(x => x.Urgency).InclusiveBetween(0, 10);
        RuleFor(x => x.Confidence).InclusiveBetween(0, 10);
    }
}
