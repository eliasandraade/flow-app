using FluentValidation;
using Flow.Application.Guidelines.Commands.CreateGuideline;
using Flow.Application.Guidelines.Commands.UpdateGuideline;

namespace Flow.Application.Common.Validation;

public sealed class CreateGuidelineCommandValidator : AbstractValidator<CreateGuidelineCommand>
{
    public CreateGuidelineCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Campaign).MaximumLength(120);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.ValidUntil)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidUntil.HasValue)
            .WithMessage("A data final deve ser posterior à data inicial.");
    }
}

public sealed class UpdateGuidelineCommandValidator : AbstractValidator<UpdateGuidelineCommand>
{
    public UpdateGuidelineCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Campaign).MaximumLength(120);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.ValidUntil)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidUntil.HasValue)
            .WithMessage("A data final deve ser posterior à data inicial.");
    }
}
