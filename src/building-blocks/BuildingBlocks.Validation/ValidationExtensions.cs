using System.Reflection;
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
        var assembly = options.ApplicationAssembly
            ?? throw new InvalidOperationException("Set WolverineOptions.ApplicationAssembly before UseBuildingBlocksValidation.");

        options.Policies.MessageFailureLogLevel(LogLevel.None);
        options.Policies.OnException<ValidationException>().MoveToErrorQueue();
        options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
        RegisterValidators(options.Services, assembly);
        options.Services.TryAdd(ServiceDescriptor.Scoped(typeof(ICommandValidation<>), typeof(CommandValidation<>)));
        options.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionProblemMapper, ValidationProblemMapper>());

        return options;
    }

    private static void RegisterValidators(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            if (CommandTypeOf(type) is { } command)
            {
                services.AddScoped(typeof(CommandValidator<>).MakeGenericType(command), type);
            }
            else if (MessageTypeOf(type) is { } message)
            {
                services.AddScoped(typeof(IValidator<>).MakeGenericType(message), type);
            }
        }
    }

    private static Type? CommandTypeOf(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(CommandValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Type? MessageTypeOf(Type type) =>
        type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
}
