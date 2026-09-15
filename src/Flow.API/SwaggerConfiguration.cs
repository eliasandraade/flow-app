using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Flow.API;

/// <summary>
/// OpenAPI description of the whole API surface. This is a delivery artefact, not an
/// afterthought: the generated specification is exported and used to document the API.
/// </summary>
public static class SwaggerConfiguration
{
    public static void Configure(SwaggerGenOptions c)
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Flow API",
            Version = "v1",
            Description =
                "Corporate innovation lifecycle platform: strategy, ideas, prioritisation, "
                + "projects, results and executive insight.\n\n"
                + "Every state-changing operation is audited, and project transitions also "
                + "produce an immutable snapshot, both written in the same transaction as "
                + "the change itself.\n\n"
                + "Errors follow RFC 7807 (application/problem+json) and always carry a "
                + "`traceId` matching the distributed trace and the audit record.",
            Contact = new OpenApiContact { Name = "Flow" }
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token obtained from POST /api/v1/auth/login."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                        { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });

        c.SupportNonNullableReferenceTypes();
        c.CustomSchemaIds(SchemaIdFor);
    }

    /// <summary>
    /// Readable, collision-free schema names.
    ///
    /// Taking only the last name segment reads nicely but breaks the moment two
    /// controllers declare a nested request type with the same name — which is exactly
    /// what happened with CompareIdeasRequest on both IdeasController and
    /// AssistantController, and it fails at generation time, producing no specification at
    /// all. Nested types therefore keep their declaring type as a prefix.
    /// </summary>
    private static string SchemaIdFor(Type type)
    {
        var name = type.IsNested && type.DeclaringType is not null
            ? $"{Simplify(type.DeclaringType.Name)}{Simplify(type.Name)}"
            : Simplify(type.Name);

        if (!type.IsGenericType) return name;

        // Generic arguments are appended so List<Foo> and List<Bar> stay distinct.
        var arguments = string.Join("", type.GetGenericArguments().Select(SchemaIdFor));
        return $"{name.Split('`')[0]}{arguments}";
    }

    private static string Simplify(string name)
    {
        var trimmed = name.Split('`')[0];

        // "IdeasController" adds nothing to "IdeasControllerUpdateIdeaRequest".
        return trimmed.EndsWith("Controller", StringComparison.Ordinal)
            ? trimmed[..^"Controller".Length]
            : trimmed;
    }
}
