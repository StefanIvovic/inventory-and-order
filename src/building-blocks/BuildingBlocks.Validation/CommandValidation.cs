using FluentValidation;
using FluentValidation.Results;

namespace BuildingBlocks.Validation;

public sealed class CommandValidation<T>(IEnumerable<CommandValidator<T>> validators) : ICommandValidation<T>
{
    public async Task EnsureValidAsync(T command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
