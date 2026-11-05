using MudBlazor;

namespace Estoka.Web.Components.Layout;

public static class EstokaTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0F766E",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#F59E0B",
            Tertiary = "#6366F1",
            Info = "#0EA5E9",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            Background = "#F4F6F8",
            BackgroundGray = "#EEF1F4",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A",
            DrawerBackground = "#0B1F24",
            DrawerText = "#C7D2D4",
            DrawerIcon = "#8FB3B0",
            TextPrimary = "#0F172A",
            TextSecondary = "#5B6B79",
            LinesDefault = "#E3E8EE",
            TableLines = "#EDF0F3",
            Divider = "#E3E8EE",
            ActionDefault = "#5B6B79",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2DD4BF",
            PrimaryContrastText = "#04201D",
            Secondary = "#FBBF24",
            Tertiary = "#A5B4FC",
            Info = "#38BDF8",
            Success = "#4ADE80",
            Warning = "#FBBF24",
            Error = "#F87171",
            Background = "#0B1215",
            BackgroundGray = "#11191D",
            Surface = "#141E22",
            AppbarBackground = "#141E22",
            AppbarText = "#E6EDF0",
            DrawerBackground = "#081316",
            DrawerText = "#B7C6C9",
            DrawerIcon = "#6FA39E",
            TextPrimary = "#E6EDF0",
            TextSecondary = "#93A4AC",
            LinesDefault = "#24323A",
            TableLines = "#1E2A30",
            Divider = "#24323A",
            ActionDefault = "#93A4AC",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "64px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
            },
            H5 = new H5Typography { FontWeight = "700", LetterSpacing = "-0.01em" },
            H6 = new H6Typography { FontWeight = "600" },
            Button = new ButtonTypography { TextTransform = "none", FontWeight = "600" },
        },
    };
}
