using System.Globalization;
using Flow.Application.Assistant;
using Flow.Application.Auth;
using Flow.Application.Common.Authorization;
using Flow.Application.Common.Behaviors;
using Flow.Application.Common.Validation;
using Flow.Application.Common.Services;
using Flow.Application.Projects;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Validation messages are read by the end user, and the product is Brazilian.
        // FluentValidation ships pt-BR translations of its built-in rules, so this one
        // line covers every NotEmpty, MaximumLength and InclusiveBetween in the assembly.
        // Code, identifiers and log messages stay in English.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.DisplayNameResolver = FieldLabels.Resolve;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<ResourceAccessPolicy>();
        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<AuditTrail>();
        services.AddScoped<AssistantRunRecorder>();
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<ProjectTransitionRecorder>();

        return services;
    }
}
