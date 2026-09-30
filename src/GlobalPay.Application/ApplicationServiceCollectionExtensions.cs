using System.Reflection;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Validation;
using GlobalPay.Application.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalPay.Application;

public static class ApplicationServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaceDefinitions =
    [
        typeof(IRequestHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.AddRequestHandlers(assembly);
        services.AddRequestValidators(assembly);
        services.AddModules(assembly);

        return services;
    }

    private static void AddRequestHandlers(this IServiceCollection services, Assembly assembly)
    {
        var registrations =
            from type in assembly.DefinedTypes
            where type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
            from @interface in type.ImplementedInterfaces
            where @interface.IsGenericType
                  && HandlerInterfaceDefinitions.Contains(@interface.GetGenericTypeDefinition())
            select (Service: @interface, Implementation: type.AsType());

        foreach (var registration in registrations)
        {
            services.AddScoped(registration.Service, registration.Implementation);
        }
    }

    private static void AddRequestValidators(this IServiceCollection services, Assembly assembly)
    {
        var registrations =
            from type in assembly.DefinedTypes
            where type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
            from @interface in type.ImplementedInterfaces
            where @interface.IsGenericType
                  && @interface.GetGenericTypeDefinition() == typeof(IRequestValidator<>)
            select (Service: @interface, Implementation: type.AsType());

        foreach (var registration in registrations)
        {
            services.AddScoped(registration.Service, registration.Implementation);
        }
    }

    private static void AddModules(this IServiceCollection services, Assembly assembly)
    {
        var moduleTypes = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IModule).IsAssignableFrom(type))
            .Select(type => type.AsType());

        foreach (var moduleType in moduleTypes)
        {
            var module = (IModule)Activator.CreateInstance(moduleType)!;
            module.Register(services);
        }
    }
}
