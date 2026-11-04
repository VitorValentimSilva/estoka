namespace Estoka.Domain.Common;

/// <summary>Validação e formatação de CPF/CNPJ (dígitos verificadores módulo 11).</summary>
public static class DocumentValidator
{
    public static string OnlyDigits(string? value)
        => value is null ? string.Empty : new string(value.Where(char.IsAsciiDigit).ToArray());

    public static bool IsValidCpf(string? value)
    {
        var d = OnlyDigits(value);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        return d[9] - '0' == CpfDigit(d, 9) && d[10] - '0' == CpfDigit(d, 10);
    }

    public static bool IsValidCnpj(string? value)
    {
        var d = OnlyDigits(value);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        return d[12] - '0' == CnpjDigit(d, 12) && d[13] - '0' == CnpjDigit(d, 13);
    }

    /// <summary>Calcula o dígito verificador do CPF considerando as <paramref name="length"/> primeiras posições.</summary>
    public static int CpfDigit(string digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (digits[i] - '0') * (length + 1 - i);
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    /// <summary>Calcula o dígito verificador do CNPJ considerando as <paramref name="length"/> primeiras posições.</summary>
    public static int CnpjDigit(string digits, int length)
    {
        int[] weights = length == 12
            ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
            : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (digits[i] - '0') * weights[i];
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    public static string FormatCpf(string digits)
        => digits.Length == 11 ? $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}" : digits;

    public static string FormatCnpj(string digits)
        => digits.Length == 14 ? $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}" : digits;

    public static string Format(string? digits)
    {
        var d = OnlyDigits(digits);
        return d.Length switch
        {
            11 => FormatCpf(d),
            14 => FormatCnpj(d),
            _ => d,
        };
    }
}
