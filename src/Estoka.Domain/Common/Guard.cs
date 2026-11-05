namespace Estoka.Domain.Common;

internal static class Guard
{
    public static string NotEmpty(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} é obrigatório.");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} deve ter no máximo {maxLength} caracteres.");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} deve ter no máximo {maxLength} caracteres.");
        return trimmed;
    }

    public static decimal NotNegative(decimal value, string field)
        => value < 0 ? throw new DomainException($"{field} não pode ser negativo.") : value;

    public static decimal Positive(decimal value, string field)
        => value <= 0 ? throw new DomainException($"{field} deve ser maior que zero.") : value;
}
