using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Estoka.Web.Api;

internal static class OpenApiSetup
{
    /// <summary>OpenAPI nativo do .NET (somente rotas /api), com o esquema de segurança da API key.</summary>
    public static IServiceCollection AddEstokaOpenApi(this IServiceCollection services)
        => services.AddOpenApi("v1", options =>
        {
            options.ShouldInclude = d => d.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Estoka API",
                    Version = "v1",
                    Description = "API REST do Estoka (estoque e vendas). Autentique-se com o cabeçalho `X-Api-Key`. " +
                                  "Na demonstração, a chave é `estoka-demo-key`.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[ApiKeyAuthOptions.Scheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = ApiKeyAuthOptions.HeaderName,
                    Description = "Chave de acesso da API",
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(ApiKeyAuthOptions.Scheme, document)] = [] });
                return Task.CompletedTask;
            });
        });
}
