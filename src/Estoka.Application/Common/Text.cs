namespace Estoka.Application.Common;

internal static class Text
{
    /// <summary>Padrão ILIKE com escape de curingas.</summary>
    public static string Like(string term)
        => "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
