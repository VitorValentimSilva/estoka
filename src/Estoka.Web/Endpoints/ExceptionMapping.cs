using Estoka.Application.Common;
using Estoka.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Estoka.Web.Endpoints;

/// <summary>Converte exceções de negócio em ProblemDetails (RFC 9457) nos endpoints HTTP.</summary>
internal sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails, ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // O middleware já reescreveu o path para /erro: usamos o path original da requisição.
        var path = new PathString(context.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? context.Request.Path);

        // Páginas Blazor seguem para a página de erro; tratamos apenas API e downloads.
        if (!IsApiOrDownload(path)) return false;

        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Conflito de concorrência"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno"),
        };
        if (status == 500) logger.LogError(exception, "Erro não tratado em {Path}", path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception switch
            {
                ValidationException validation => string.Join(" ", validation.Errors.Select(e => e.ErrorMessage).Distinct()),
                _ when status == 500 => "Ocorreu um erro inesperado.",
                _ => exception.Message,
            },
            Instance = path,
        };
        if (exception is ValidationException v)
            problem.Extensions["errors"] = v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }

    public static bool IsApiOrDownload(PathString path)
        => path.StartsWithSegments("/api")
           || path.StartsWithSegments("/relatorios/exportar")
           || (path.Value?.EndsWith("/pdf", StringComparison.Ordinal) ?? false);
}
