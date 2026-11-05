using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Estoka.Web.Infrastructure;

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Tentativas de login (POST /login) por IP por minuto.</summary>
    public int LoginPerMinute { get; set; } = 10;

    /// <summary>Requisições à API (/api) por IP por minuto.</summary>
    public int ApiPerMinute { get; set; } = 120;
}

internal static class SecuritySetup
{
    /// <summary>
    /// Limite de requisições por IP: protege o login contra força bruta distribuída por contas
    /// (o lockout do Identity é por conta) e a API contra abuso. Demais rotas não são limitadas.
    /// </summary>
    public static IServiceCollection AddEstokaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
                if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => Window(options.LoginPerMinute));
                if (context.Request.Path.StartsWithSegments("/api"))
                    return RateLimitPartition.GetFixedWindowLimiter($"api:{ip}", _ => Window(options.ApiPerMinute));
                return RateLimitPartition.GetNoLimiter("livre");
            });
            limiter.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (ctx.HttpContext.Request.Path.StartsWithSegments("/api"))
                {
                    await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = ctx.HttpContext,
                        ProblemDetails = { Status = 429, Title = "Muitas requisições", Detail = "Limite de requisições atingido. Tente novamente em instantes." },
                    });
                    return;
                }
                ctx.HttpContext.Response.ContentType = "text/html; charset=utf-8";
                await ctx.HttpContext.Response.WriteAsync(
                    "<!doctype html><meta charset=utf-8><title>Muitas tentativas</title>" +
                    "<p style=\"font-family:system-ui;padding:2rem\">Muitas tentativas de login em pouco tempo. Aguarde um minuto e " +
                    "<a href=\"/login\">tente novamente</a>.</p>", ct);
            };
        });
        return services;

        static FixedWindowRateLimiterOptions Window(int permits) => new()
        {
            PermitLimit = Math.Max(permits, 1), Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
        };
    }

    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +           // importmap inline do Blazor
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " + // MudBlazor usa estilos inline
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +                           // SignalR/WebSocket na mesma origem
        "base-uri 'self'; form-action 'self'; frame-ancestors 'self'; object-src 'none'";

    /// <summary>Cabeçalhos de segurança em todas as respostas (a UI do Scalar/OpenAPI fica sem CSP própria).</summary>
    public static IApplicationBuilder UseEstokaSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            // Substitui o "frame-ancestors 'self'" que o Blazor envia por uma política completa (que o inclui).
            if (!context.Request.Path.StartsWithSegments("/scalar") && !context.Request.Path.StartsWithSegments("/openapi"))
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            return Task.CompletedTask;
        });
        return next(context);
    });
}
