using FluentValidation;

namespace Estoka.Application.Common;

public static class ValidationExtensions
{
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid) throw new ValidationException(result.Errors);
    }

    public static IRuleBuilderOptions<T, string?> ValidPhone<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(v => string.IsNullOrWhiteSpace(v) || Domain.Common.DocumentValidator.OnlyDigits(v).Length is 10 or 11)
            .WithMessage("Telefone deve ter DDD + 8 ou 9 dígitos.");

    public static IRuleBuilderOptions<T, string?> ValidCnpj<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(Domain.Common.DocumentValidator.IsValidCnpj).WithMessage("CNPJ inválido.");

    public static IRuleBuilderOptions<T, string?> ValidState<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(v => string.IsNullOrWhiteSpace(v) || BrazilianStates.All.Contains(v.Trim().ToUpperInvariant()))
            .WithMessage("UF inválida.");
}

public static class BrazilianStates
{
    public static readonly string[] All =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR",
        "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];
}
