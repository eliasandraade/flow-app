using FluentValidation;
using MediatR;
using ValidationException = Flow.Application.Common.Exceptions.ValidationException;

namespace Flow.Application.Common.Behaviors;

/// <summary>
/// Runs FluentValidation validators before the handler and converts failures into the
/// application's own ValidationException, which the API surfaces as RFC7807 with a 422.
///
/// FluentValidation was already a dependency of this project but had no validators and no
/// pipeline wiring, so input validation rested entirely on model binding and on domain
/// invariants throwing later than they should.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        throw new ValidationException(errors);
    }
}
