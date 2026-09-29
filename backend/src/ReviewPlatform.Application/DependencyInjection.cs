using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;

namespace ReviewPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<EmployeeReader>();
        services.AddScoped<EmployeeRules>();
        return services;
    }
}
