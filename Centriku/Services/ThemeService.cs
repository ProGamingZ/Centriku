using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Centriku.Services
{
    public record ThemeModeTokens(
        string AppBackground,
        string SurfaceBrush,
        string SurfaceVariantBrush,
        string InputBgBrush,
        string InputFocusBgBrush,
        string TextMainBrush,
        string TextMutedBrush,
        string BorderBrush,
        string BorderStrongBrush,
        string PrimaryBrush,
        string PrimaryHoverBrush,
        string PrimaryBgBrush,
        string AccentBrush,
        string AccentBgBrush,
        string TsColumnBrush,
        string WsColumnBrush,
        string DataGridAltRowBrush,
        string DataGridHoverBrush,
        string DataGridSelectedBrush,
        string ScrollBarThumbBrush,
        string ScrollBarThumbHoverBrush
    );

    public record ThemePalette(string Name, ThemeModeTokens Light, ThemeModeTokens Dark);

    public static class ThemeService
    {
        // =========================================================================
        // TO ADD OR REMOVE THEMES IN THE FUTURE, SIMPLY EDIT THIS LIST!
        // =========================================================================
        public static readonly IReadOnlyList<ThemePalette> Palettes =
        [
            new ThemePalette(
                "NwSSU Forest",
                Light: new(
                    AppBackground: "#EFF5F1", SurfaceBrush: "#FFFFFF", SurfaceVariantBrush: "#E3EFE7",
                    InputBgBrush: "#FFFFFF", InputFocusBgBrush: "#F0FDF4",
                    TextMainBrush: "#0D1F14", TextMutedBrush: "#3F5A49",
                    BorderBrush: "#C5D8CC", BorderStrongBrush: "#86A893",
                    PrimaryBrush: "#166534", PrimaryHoverBrush: "#15803D", PrimaryBgBrush: "#1F166534",
                    AccentBrush: "#EA580C", AccentBgBrush: "#1FEA580C",
                    TsColumnBrush: "#0F766E", WsColumnBrush: "#EA580C",
                    DataGridAltRowBrush: "#F4F9F5", DataGridHoverBrush: "#E3EFE7", DataGridSelectedBrush: "#DCFCE7",
                    ScrollBarThumbBrush: "#86A893", ScrollBarThumbHoverBrush: "#166534"
                ),
                Dark: new(
                    AppBackground: "#09140E", SurfaceBrush: "#112218", SurfaceVariantBrush: "#193124",
                    InputBgBrush: "#0C1A12", InputFocusBgBrush: "#142E20",
                    TextMainBrush: "#F2F9F5", TextMutedBrush: "#86A895",
                    BorderBrush: "#234231", BorderStrongBrush: "#346148",
                    PrimaryBrush: "#16A34A", PrimaryHoverBrush: "#22C55E", PrimaryBgBrush: "#2616A34A",
                    AccentBrush: "#FB923C", AccentBgBrush: "#24FB923C",
                    TsColumnBrush: "#2DD4BF", WsColumnBrush: "#FB923C",
                    DataGridAltRowBrush: "#0D1B13", DataGridHoverBrush: "#1C3829", DataGridSelectedBrush: "#1E4631",
                    ScrollBarThumbBrush: "#346148", ScrollBarThumbHoverBrush: "#22C55E"
                )
            ),

            new ThemePalette(
                "Royal Purple",
                Light: new(
                    AppBackground: "#EDF2F7", SurfaceBrush: "#F8FAFC", SurfaceVariantBrush: "#E2E8F0",
                    InputBgBrush: "#FFFFFF", InputFocusBgBrush: "#F5F3FF",
                    TextMainBrush: "#0F172A", TextMutedBrush: "#475569",
                    BorderBrush: "#CBD5E1", BorderStrongBrush: "#94A3B8",
                    PrimaryBrush: "#6D28D9", PrimaryHoverBrush: "#7C3AED", PrimaryBgBrush: "#1F6D28D9",
                    AccentBrush: "#D97706", AccentBgBrush: "#1FD97706",
                    TsColumnBrush: "#7C3AED", WsColumnBrush: "#D97706",
                    DataGridAltRowBrush: "#F1F5F9", DataGridHoverBrush: "#E2E8F0", DataGridSelectedBrush: "#DDD6FE",
                    ScrollBarThumbBrush: "#94A3B8", ScrollBarThumbHoverBrush: "#6D28D9"
                ),
                Dark: new(
                    AppBackground: "#0F172A", SurfaceBrush: "#1E293B", SurfaceVariantBrush: "#26334D",
                    InputBgBrush: "#131C2E", InputFocusBgBrush: "#1A2540",
                    TextMainBrush: "#F8FAFC", TextMutedBrush: "#94A3B8",
                    BorderBrush: "#334155", BorderStrongBrush: "#475569",
                    PrimaryBrush: "#7C3AED", PrimaryHoverBrush: "#8B5CF6", PrimaryBgBrush: "#267C3AED",
                    AccentBrush: "#FBBF24", AccentBgBrush: "#24FBBF24",
                    TsColumnBrush: "#A78BFA", WsColumnBrush: "#FBBF24",
                    DataGridAltRowBrush: "#172033", DataGridHoverBrush: "#28354E", DataGridSelectedBrush: "#3B276B",
                    ScrollBarThumbBrush: "#475569", ScrollBarThumbHoverBrush: "#8B5CF6"
                )
            ),

            new ThemePalette(
                "Ocean Blue",
                Light: new(
                    AppBackground: "#EFF6FF", SurfaceBrush: "#FFFFFF", SurfaceVariantBrush: "#DBEAFE",
                    InputBgBrush: "#FFFFFF", InputFocusBgBrush: "#EFF6FF",
                    TextMainBrush: "#0F172A", TextMutedBrush: "#334155",
                    BorderBrush: "#BFDBFE", BorderStrongBrush: "#93C5FD",
                    PrimaryBrush: "#2563EB", PrimaryHoverBrush: "#1D4ED8", PrimaryBgBrush: "#1F2563EB",
                    AccentBrush: "#0D9488", AccentBgBrush: "#1F0D9488",
                    TsColumnBrush: "#0D9488", WsColumnBrush: "#D97706",
                    DataGridAltRowBrush: "#F8FAFC", DataGridHoverBrush: "#DBEAFE", DataGridSelectedBrush: "#BFDBFE",
                    ScrollBarThumbBrush: "#93C5FD", ScrollBarThumbHoverBrush: "#2563EB"
                ),
                Dark: new(
                    AppBackground: "#0B132B", SurfaceBrush: "#152238", SurfaceVariantBrush: "#1E3252",
                    InputBgBrush: "#0E182E", InputFocusBgBrush: "#1A2C4E",
                    TextMainBrush: "#F8FAFC", TextMutedBrush: "#94A3B8",
                    BorderBrush: "#263B5E", BorderStrongBrush: "#3B5988",
                    PrimaryBrush: "#3B82F6", PrimaryHoverBrush: "#60A5FA", PrimaryBgBrush: "#263B82F6",
                    AccentBrush: "#2DD4BF", AccentBgBrush: "#242DD4BF",
                    TsColumnBrush: "#2DD4BF", WsColumnBrush: "#FBBF24",
                    DataGridAltRowBrush: "#101B30", DataGridHoverBrush: "#1E3252", DataGridSelectedBrush: "#1E3A8A",
                    ScrollBarThumbBrush: "#3B5988", ScrollBarThumbHoverBrush: "#60A5FA"
                )
            ),

            new ThemePalette(
                "Teal & Slate",
                Light: new(
                    AppBackground: "#F0FDFA", SurfaceBrush: "#FFFFFF", SurfaceVariantBrush: "#CCFBF1",
                    InputBgBrush: "#FFFFFF", InputFocusBgBrush: "#F0FDFA",
                    TextMainBrush: "#0F172A", TextMutedBrush: "#334155",
                    BorderBrush: "#99F6E4", BorderStrongBrush: "#5EEAD4",
                    PrimaryBrush: "#0F766E", PrimaryHoverBrush: "#0D9488", PrimaryBgBrush: "#1F0F766E",
                    AccentBrush: "#EA580C", AccentBgBrush: "#1FEA580C",
                    TsColumnBrush: "#0284C7", WsColumnBrush: "#EA580C",
                    DataGridAltRowBrush: "#F6FEFC", DataGridHoverBrush: "#CCFBF1", DataGridSelectedBrush: "#99F6E4",
                    ScrollBarThumbBrush: "#5EEAD4", ScrollBarThumbHoverBrush: "#0F766E"
                ),
                Dark: new(
                    AppBackground: "#071717", SurfaceBrush: "#0F2929", SurfaceVariantBrush: "#163B3B",
                    InputBgBrush: "#0A1F1F", InputFocusBgBrush: "#143535",
                    TextMainBrush: "#F0FDFA", TextMutedBrush: "#99F6E4",
                    BorderBrush: "#1F4E4E", BorderStrongBrush: "#2D6A6A",
                    PrimaryBrush: "#14B8A6", PrimaryHoverBrush: "#2DD4BF", PrimaryBgBrush: "#2614B8A6",
                    AccentBrush: "#FB923C", AccentBgBrush: "#24FB923C",
                    TsColumnBrush: "#38BDF8", WsColumnBrush: "#FB923C",
                    DataGridAltRowBrush: "#0B2020", DataGridHoverBrush: "#163B3B", DataGridSelectedBrush: "#115E59",
                    ScrollBarThumbBrush: "#2D6A6A", ScrollBarThumbHoverBrush: "#2DD4BF"
                )
            ),

            new ThemePalette(
                "Crimson Rose",
                Light: new(
                    AppBackground: "#FFF1F2", SurfaceBrush: "#FFFFFF", SurfaceVariantBrush: "#FFE4E6",
                    InputBgBrush: "#FFFFFF", InputFocusBgBrush: "#FFF1F2",
                    TextMainBrush: "#1F1216", TextMutedBrush: "#5C3A42",
                    BorderBrush: "#FECDD3", BorderStrongBrush: "#FDA4AF",
                    PrimaryBrush: "#BE123C", PrimaryHoverBrush: "#E11D48", PrimaryBgBrush: "#1FBE123C",
                    AccentBrush: "#D97706", AccentBgBrush: "#1FD97706",
                    TsColumnBrush: "#0F766E", WsColumnBrush: "#D97706",
                    DataGridAltRowBrush: "#FFF7F8", DataGridHoverBrush: "#FFE4E6", DataGridSelectedBrush: "#FECDD3",
                    ScrollBarThumbBrush: "#FDA4AF", ScrollBarThumbHoverBrush: "#BE123C"
                ),
                Dark: new(
                    AppBackground: "#160A0E", SurfaceBrush: "#261219", SurfaceVariantBrush: "#381A25",
                    InputBgBrush: "#1C0D12", InputFocusBgBrush: "#301620",
                    TextMainBrush: "#FFF1F2", TextMutedBrush: "#FDA4AF",
                    BorderBrush: "#4C2332", BorderStrongBrush: "#6E3248",
                    PrimaryBrush: "#F43F5E", PrimaryHoverBrush: "#FB7185", PrimaryBgBrush: "#26F43F5E",
                    AccentBrush: "#FBBF24", AccentBgBrush: "#24FBBF24",
                    TsColumnBrush: "#2DD4BF", WsColumnBrush: "#FBBF24",
                    DataGridAltRowBrush: "#1E0E14", DataGridHoverBrush: "#381A25", DataGridSelectedBrush: "#881337",
                    ScrollBarThumbBrush: "#6E3248", ScrollBarThumbHoverBrush: "#FB7185"
                )
            )
        ];

        public static IReadOnlyList<string> GetThemeNames() => Palettes.Select(p => p.Name).ToList();

        public static void ApplyTheme(string? themeName, bool isDark)
        {
            var app = Application.Current;
            if (app == null) return;

            var palette = Palettes.FirstOrDefault(p => p.Name == themeName) ?? Palettes[0];

            ApplyTokensToDictionary(app.Resources, ThemeVariant.Light, palette.Light);
            ApplyTokensToDictionary(app.Resources, ThemeVariant.Dark, palette.Dark);

            // Switch theme variant cleanly to force all DynamicResources to refresh
            app.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static void ApplyTokensToDictionary(IResourceDictionary rootResources, ThemeVariant variant, ThemeModeTokens t)
        {
            if (!rootResources.ThemeDictionaries.TryGetValue(variant, out var provider) || provider is not IResourceDictionary dict)
                return;

            void SetBrush(string key, string hex) => dict[key] = new SolidColorBrush(Color.Parse(hex));

            SetBrush("AppBackground", t.AppBackground);
            SetBrush("SurfaceBrush", t.SurfaceBrush);
            SetBrush("SurfaceVariantBrush", t.SurfaceVariantBrush);
            SetBrush("InputBgBrush", t.InputBgBrush);
            SetBrush("InputFocusBgBrush", t.InputFocusBgBrush);
            SetBrush("TextMainBrush", t.TextMainBrush);
            SetBrush("TextMutedBrush", t.TextMutedBrush);
            SetBrush("BorderBrush", t.BorderBrush);
            SetBrush("BorderStrongBrush", t.BorderStrongBrush);
            SetBrush("PrimaryBrush", t.PrimaryBrush);
            SetBrush("PrimaryHoverBrush", t.PrimaryHoverBrush);
            SetBrush("PrimaryBgBrush", t.PrimaryBgBrush);
            SetBrush("AccentBrush", t.AccentBrush);
            SetBrush("AccentBgBrush", t.AccentBgBrush);
            SetBrush("TsColumnBrush", t.TsColumnBrush);
            SetBrush("WsColumnBrush", t.WsColumnBrush);
            SetBrush("DataGridAltRowBrush", t.DataGridAltRowBrush);
            SetBrush("DataGridHoverBrush", t.DataGridHoverBrush);
            SetBrush("DataGridSelectedBrush", t.DataGridSelectedBrush);
            SetBrush("ScrollBarThumbBrush", t.ScrollBarThumbBrush);
            SetBrush("ScrollBarThumbHoverBrush", t.ScrollBarThumbHoverBrush);
            SetBrush("ScrollBarThumbFill", t.ScrollBarThumbBrush);
            SetBrush("ScrollBarThumbFillPointerOver", t.ScrollBarThumbHoverBrush);
            SetBrush("ScrollBarThumbFillPressed", t.PrimaryBrush);
        }
    }
}