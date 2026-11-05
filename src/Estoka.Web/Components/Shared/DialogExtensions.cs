using MudBlazor;

namespace Estoka.Web.Components.Shared;

public static class DialogExtensions
{
    public static readonly DialogOptions FormOptions = new()
    {
        MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true, BackdropClick = false, CloseOnEscapeKey = true,
    };

    public static readonly DialogOptions SmallOptions = new()
    {
        MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true,
    };

    /// <summary>Abre o diálogo de confirmação. Retorna o motivo (ou string vazia) se confirmado; null se cancelado.</summary>
    public static async Task<string?> ConfirmAsync(this IDialogService dialogs, string title, string message,
        string confirmText = "Excluir", Color color = Color.Error, bool askReason = false)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, title },
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.Color, color },
            { x => x.AskReason, askReason },
        };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>(title, parameters, SmallOptions);
        var result = await dialog.Result;
        return result is { Canceled: false } ? result.Data as string ?? string.Empty : null;
    }
}
