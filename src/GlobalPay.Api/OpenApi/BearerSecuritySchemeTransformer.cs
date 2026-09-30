using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GlobalPay.Api.OpenApi;

/// <summary>
/// Adds the bearer security scheme to the generated OpenAPI document so Swagger UI and Scalar expose an
/// "Authorize" control for Keycloak / Entra ID access tokens.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    internal const string SchemeName = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = """
                Paste an access token obtained from the identity provider.

                Local development (Keycloak):
                  POST http://localhost:8080/realms/globalpay/protocol/openid-connect/token
                  grant_type=password&client_id=globalpay-cli&username=admin.user&password=Password123!

                Use the returned 'access_token' value. Swagger UI adds the "Bearer " prefix for you.
                """,
        };

        document.Security ??= [];

        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
