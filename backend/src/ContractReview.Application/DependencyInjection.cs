using ContractReview.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        var types = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        {
            foreach (var iface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(iface, type);
            }
        }

        return services;
    }
}
