using System.Globalization;
using Estoka.Domain.Common;
using FluentValidation;
using Severity = MudBlazor.Severity;
using MudBlazor;

namespace Estoka.Web.Infrastructure;

public static class UiExtensions
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(this decimal value) => value.ToString("C2", PtBr);
    public static string Money(this decimal? value) => value is { } v ? v.ToString("C2", PtBr) : "—";
    public static string Qty(this decimal value) => value.ToString(value % 1 == 0 ? "N0" : "N2", PtBr);
    public static string Percent(this decimal value) => (value / 100).ToString("P1", PtBr);

    public static string Local(this DateTimeOffset value, string format = "dd/MM/yyyy HH:mm")
        => Application.Common.BrazilTime.ToLocal(value).ToString(format, PtBr);

    public static string Local(this DateTimeOffset? value, string format = "dd/MM/yyyy HH:mm")
        => value is { } v ? v.Local(format) : "—";

    public static string Phone(string? digits)
    {
        var d = DocumentValidator.OnlyDigits(digits);
        return d.Length switch
        {
            11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
            10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
            _ => d,
        };
    }

    public static string ZipCode(string? digits)
    {
        var d = DocumentValidator.OnlyDigits(digits);
        return d.Length == 8 ? $"{d[..5]}-{d[5..]}" : d;
    }

    /// <summary>Adaptador FluentValidation → MudForm (valida só o campo alterado).</summary>
    public static Func<object, string, Task<IEnumerable<string>>> ForMudForm<T>(this IValidator<T> validator)
        => async (model, propertyName) =>
        {
            var result = await validator.ValidateAsync(ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));
            return result.IsValid ? [] : result.Errors.Select(e => e.ErrorMessage);
        };

    /// <summary>Executa uma ação de UI exibindo snackbar de sucesso ou de erro amigável.</summary>
    public static async Task<bool> RunAsync(this ISnackbar snackbar, Func<Task> action, string? success = null, ILogger? logger = null)
    {
        try
        {
            await action();
            if (success is not null) snackbar.Add(success, Severity.Success);
            return true;
        }
        catch (Exception ex)
        {
            if (!ErrorMessages.IsExpected(ex)) logger?.LogError(ex, "Erro inesperado na interface");
            snackbar.Add(ErrorMessages.Friendly(ex), ex is Application.Common.ConcurrencyConflictException ? Severity.Warning : Severity.Error);
            return false;
        }
    }

    public static readonly PatternMask CpfMask = new("000.000.000-00");
    public static readonly PatternMask CnpjMask = new("00.000.000/0000-00");
    public static readonly PatternMask CepMask = new("00000-000");

    /// <summary>Fixo (00) 0000-0000; muda para celular (00) 00000-0000 quando o número começa com 9.</summary>
    public static MultiMask PhoneMask => new("(00) 0000-0000", new MaskOption("Celular", "(00) 00000-0000", @"^\(\d\d\) 9"));
}
