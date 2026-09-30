using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Application.Employees;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Notifications;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Application.Surveys;

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
        services.AddScoped<SessionAccess>();
        services.AddScoped<SessionReader>();
        services.AddScoped<IndicatorSnapshotBuilder>();
        services.AddScoped<SurveyLoader>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<SessionNotifier>();
        services.AddScoped<MatrixEditor>();
        services.AddScoped<MatrixImportPlanner>();
        return services;
    }
}
