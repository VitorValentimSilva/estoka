namespace Estoka.Domain.Common;

/// <summary>
/// Códigos de barras GTIN (EAN-8, UPC-A, EAN-13, GTIN-14) com dígito verificador módulo 10 (GS1).
/// </summary>
public static class Gtin
{
    public static readonly int[] ValidLengths = [8, 12, 13, 14];

    /// <summary>Remove espaços/traços; retorna null se vazio.</summary>
    public static string? Normalize(string? value)
    {
        var digits = DocumentValidator.OnlyDigits(value);
        return digits.Length == 0 ? null : digits;
    }

    public static bool IsValid(string? value)
    {
        if (value is null || value.Any(c => !char.IsAsciiDigit(c) && c != ' ' && c != '-')) return false;
        var digits = DocumentValidator.OnlyDigits(value);
        return ValidLengths.Contains(digits.Length) && digits[^1] - '0' == CheckDigit(digits[..^1]);
    }

    /// <summary>Dígito verificador GS1 para o "corpo" do código (sem o dígito final).</summary>
    public static int CheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            // Da direita para a esquerda: pesos 3, 1, 3, 1...
            var weight = (body.Length - i) % 2 == 1 ? 3 : 1;
            sum += (body[i] - '0') * weight;
        }
        return (10 - sum % 10) % 10;
    }
}
