using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Master_WinUI.Services;

public static class StatusInfoBarStyler
{
    public static void Apply(InfoBar infoBar, ElementTheme theme)
    {
        var isLight = theme == ElementTheme.Light;
        var foreground = CreateBrush(isLight ? "#1F2937" : "#F1F5F9");
        var backgroundColor = (isLight, infoBar.Severity) switch
        {
            (true, InfoBarSeverity.Success) => "#B8E6C0",
            (true, InfoBarSeverity.Warning) => "#FFF1B8",
            (true, InfoBarSeverity.Error) => "#B42318",
            (false, InfoBarSeverity.Success) => "#17472B",
            (false, InfoBarSeverity.Warning) => "#594313",
            (false, InfoBarSeverity.Error) => "#5C2229",
            _ => null
        };

        infoBar.Background = backgroundColor is null ? null : CreateBrush(backgroundColor);
        infoBar.Foreground = isLight && infoBar.Severity == InfoBarSeverity.Error
            ? CreateBrush("#FFFFFF")
            : foreground;
        infoBar.IconSource = isLight && infoBar.Severity == InfoBarSeverity.Error
            ? new SymbolIconSource
            {
                Symbol = Symbol.Cancel,
                Foreground = CreateBrush("#FFFFFF")
            }
            : null;
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var red = Convert.ToByte(color.Substring(1, 2), 16);
        var green = Convert.ToByte(color.Substring(3, 2), 16);
        var blue = Convert.ToByte(color.Substring(5, 2), 16);
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, red, green, blue));
    }
}
