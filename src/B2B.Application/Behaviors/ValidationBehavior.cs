using FluentValidation;
using MediatR;
using ValidationException = B2B.Domain.Exceptions.ValidationException;

namespace B2B.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation.
/// Runs all registered validators for the request type before the handler executes.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var errorMessages = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).ToArray());

            var message = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
            throw new ValidationException("Validation", message);
        }

        return await next();
    }
}
