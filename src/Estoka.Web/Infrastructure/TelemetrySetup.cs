using Estoka.Application.Common;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Estoka.Web.Infrastructure;

internal static class TelemetrySetup
{
    /// <summary>
    /// OpenTelemetry: traces (HTTP, banco via Npgsql e casos de uso), métricas (ASP.NET Core, runtime e de negócio) e logs.
    /// A exportação OTLP liga só quando OTEL_EXPORTER_OTLP_ENDPOINT está definido (ex.: Grafana Cloud, Honeycomb,
    /// Jaeger/Aspire Dashboard local) — sem ele, nada é enviado e o custo é desprezível.
    /// </summary>
    public static WebApplicationBuilder AddEstokaTelemetry(this WebApplicationBuilder builder)
    {
        var otlpEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var version = typeof(TelemetrySetup).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "estoka", serviceVersion: version)
                .AddAttributes([new("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddSource(EstokaTelemetry.Name)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                    !ctx.Request.Path.StartsWithSegments("/health") && !ctx.Request.Path.StartsWithSegments("/_framework")
                    && !ctx.Request.Path.StartsWithSegments("/_content"))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(EstokaTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (otlpEnabled)
        {
            builder.Logging.AddOpenTelemetry(o =>
            {
                o.IncludeFormattedMessage = true;
                o.IncludeScopes = true;
            });
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
