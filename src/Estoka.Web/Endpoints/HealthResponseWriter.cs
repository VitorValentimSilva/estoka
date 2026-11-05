using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Estoka.Web.Endpoints;

internal static class HealthResponseWriter
{
    /// <summary>Detalhes de erro só em desenvolvimento: o endpoint é anônimo.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report, bool includeErrors)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(e => e.Key, e => new
            {
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                error = includeErrors ? e.Value.Exception?.Message : null,
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
