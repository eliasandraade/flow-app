using FluentValidation;
using Flow.Application.Projects.Commands.BlockProject;
using Flow.Application.Projects.Commands.CancelProject;
using Flow.Application.Projects.Commands.ConvertIdeaToProject;
using Flow.Application.Projects.Commands.CreateProject;
using Flow.Application.Projects.Commands.UpdateProject;
using Flow.Application.Projects.Commands.UpdateProjectProgress;
using Flow.Application.Results.Commands.RecordResult;

namespace Flow.Application.Common.Validation;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost.HasValue);
    }
}

public sealed class ConvertIdeaToProjectCommandValidator : AbstractValidator<ConvertIdeaToProjectCommand>
{
    public ConvertIdeaToProjectCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost.HasValue);
    }
}

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost.HasValue);
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0).When(x => x.ActualCost.HasValue);
    }
}

public sealed class BlockProjectCommandValidator : AbstractValidator<BlockProjectCommand>
{
    public BlockProjectCommandValidator() =>
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Informe o motivo do bloqueio.")
            .MaximumLength(2000);
}

public sealed class CancelProjectCommandValidator : AbstractValidator<CancelProjectCommand>
{
    public CancelProjectCommandValidator() =>
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(2000);
}

public sealed class UpdateProjectProgressCommandValidator : AbstractValidator<UpdateProjectProgressCommand>
{
    public UpdateProjectProgressCommandValidator() =>
        RuleFor(x => x.ProgressPercentage).InclusiveBetween(0, 100);
}

public sealed class RecordResultCommandValidator : AbstractValidator<RecordResultCommand>
{
    public RecordResultCommandValidator()
    {
        RuleFor(x => x.EstimatedRevenue).GreaterThanOrEqualTo(0).When(x => x.EstimatedRevenue.HasValue);
        RuleFor(x => x.EstimatedSavings).GreaterThanOrEqualTo(0).When(x => x.EstimatedSavings.HasValue);
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost.HasValue);
        RuleFor(x => x.ActualRevenue).GreaterThanOrEqualTo(0).When(x => x.ActualRevenue.HasValue);
        RuleFor(x => x.ActualSavings).GreaterThanOrEqualTo(0).When(x => x.ActualSavings.HasValue);
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0).When(x => x.ActualCost.HasValue);
        RuleFor(x => x.PaybackPeriodMonths).GreaterThanOrEqualTo(0).When(x => x.PaybackPeriodMonths.HasValue);
        RuleFor(x => x.TimeSavedHours).GreaterThanOrEqualTo(0).When(x => x.TimeSavedHours.HasValue);
        RuleFor(x => x.ProductivityGainPercent).InclusiveBetween(-100, 1000).When(x => x.ProductivityGainPercent.HasValue);
        RuleFor(x => x.QualityGainPercent).InclusiveBetween(-100, 1000).When(x => x.QualityGainPercent.HasValue);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
