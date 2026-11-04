using Estoka.Application.Common;
using Estoka.Domain.Common;
using FluentValidation;

namespace Estoka.Web.Infrastructure;

public static class ErrorMessages
{
    /// <summary>Converte exceções conhecidas em mensagens amigáveis; erros inesperados viram mensagem genérica.</summary>
    public static string Friendly(Exception ex) => ex switch
    {
        ValidationException v => string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct()),
        DomainException or NotFoundException or ForbiddenException or ConcurrencyConflictException => ex.Message,
        _ => "Ocorreu um erro inesperado. Tente novamente em instantes.",
    };

    public static bool IsExpected(Exception ex)
        => ex is ValidationException or DomainException or NotFoundException or ForbiddenException or ConcurrencyConflictException;
}
