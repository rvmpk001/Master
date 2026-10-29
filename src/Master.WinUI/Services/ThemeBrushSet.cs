using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Master_WinUI.Services;

public sealed class ThemeBrushSet
{
    private static readonly IReadOnlyDictionary<string, string> LightColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["#0057A8"] = "#0057A8",
            ["#0078D4"] = "#A5D2F5",
            ["#0B1220"] = "#F5F7FA",
            ["#101B2D"] = "#E9EEF5",
            ["#111B2B"] = "#FFFFFF",
            ["#111C2D"] = "#FFFFFF",
            ["#172337"] = "#F0F3F8",
            ["#1A2638"] = "#FFFFFF",
            ["#1E2D43"] = "#F2F5F9",
            ["#26364D"] = "#E7ECF2",
            ["#263B5A"] = "#DEE6EF",
            ["#40516A"] = "#B3C0CE",
            ["#A9B8CC"] = "#586779",
            ["#E6EDF7"] = "#253244",
            ["#F1F5F9"] = "#1F2937",
            ["#EF4444"] = "#B42318",
            ["#0058A0"] = "#CFE4FF",
            ["#005EA7"] = "#CFE4FF",
            ["#22C55E"] = "#16803A",
            ["#FFBE5A"] = "#805B12",
            ["#72D49A"] = "#137A45"
        };

    private readonly Dictionary<string, SolidColorBrush> _brushes = new(StringComparer.OrdinalIgnoreCase);
    private ElementTheme _theme = ElementTheme.Dark;

    public SolidColorBrush GetBrush(string darkColor)
    {
        var key = Normalize(darkColor);
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(ParseColor(ResolveColor(key)));
            _brushes.Add(key, brush);
        }

        return brush;
    }

    public void ApplyTheme(ElementTheme theme)
    {
        _theme = theme;
        foreach (var (darkColor, brush) in _brushes)
        {
            brush.Color = ParseColor(ResolveColor(darkColor));
        }
    }

    private string ResolveColor(string darkColor)
    {
        var colorKey = darkColor.Length == 9 ? $"#{darkColor[3..]}" : darkColor;
        if (_theme != ElementTheme.Light || !LightColors.TryGetValue(colorKey, out var lightColor))
        {
            return darkColor;
        }

        return darkColor.Length == 9
            ? $"{darkColor[..3]}{lightColor[1..]}"
            : lightColor;
    }

    private static string Normalize(string color)
    {
        var normalized = color.Trim().ToUpperInvariant();
        if (normalized.Length is not (7 or 9) || normalized[0] != '#')
        {
            throw new ArgumentException("El color debe estar en formato #RRGGBB o #AARRGGBB.", nameof(color));
        }

        return normalized;
    }

    private static Windows.UI.Color ParseColor(string color)
    {
        var alpha = color.Length == 9 ? color[1..3] : "FF";
        var offset = color.Length == 9 ? 3 : 1;
        return Microsoft.UI.ColorHelper.FromArgb(
            Convert.ToByte(alpha, 16),
            Convert.ToByte(color.Substring(offset, 2), 16),
            Convert.ToByte(color.Substring(offset + 2, 2), 16),
            Convert.ToByte(color.Substring(offset + 4, 2), 16));
    }
}
