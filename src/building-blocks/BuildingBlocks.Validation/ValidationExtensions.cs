using BuildingBlocks.AspNetCore.Errors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;

namespace BuildingBlocks.Validation;

public static class ValidationExtensions
{
    public static WolverineOptions UseBuildingBlocksValidation(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Policies.MessageFailureLogLevel(LogLevel.None);
        options.Policies.OnException<ValidationException>().MoveToErrorQueue();
        options.UseFluentValidation();
        options.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionProblemMapper, ValidationProblemMapper>());

        return options;
    }
}
