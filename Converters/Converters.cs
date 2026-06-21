using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DStockAnalysis.Models;

namespace DStockAnalysis.Converters;

internal enum Quality { Neutral, Good, Ok, Caution, Bad }

internal static class Palette
{
    public static readonly Brush Good = Freeze(Color.FromArgb(0x66, 0x2E, 0x7D, 0x32));    // 緑
    public static readonly Brush Ok = Freeze(Color.FromArgb(0x66, 0x15, 0x65, 0xC0));      // 青
    public static readonly Brush Caution = Freeze(Color.FromArgb(0x66, 0xEF, 0x6C, 0x00)); // オレンジ
    public static readonly Brush Bad = Freeze(Color.FromArgb(0x66, 0xC6, 0x28, 0x28));     // 赤
    public static readonly Brush Neutral = Brushes.Transparent;

    public static readonly Brush GoodSolid = Freeze(Color.FromRgb(0x43, 0xA0, 0x47));
    public static readonly Brush OkSolid = Freeze(Color.FromRgb(0x29, 0xB6, 0xF6));
    public static readonly Brush CautionSolid = Freeze(Color.FromRgb(0xFF, 0xA7, 0x26));
    public static readonly Brush BadSolid = Freeze(Color.FromRgb(0xEF, 0x53, 0x50));
    public static readonly Brush NeutralSolid = Freeze(Color.FromRgb(0x78, 0x90, 0x9C));

    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public static Brush Bg(Quality q) => q switch
    {
        Quality.Good => Good,
        Quality.Ok => Ok,
        Quality.Caution => Caution,
        Quality.Bad => Bad,
        _ => Neutral
    };

    public static Brush Solid(Quality q) => q switch
    {
        Quality.Good => GoodSolid,
        Quality.Ok => OkSolid,
        Quality.Caution => CautionSolid,
        Quality.Bad => BadSolid,
        _ => NeutralSolid
    };
}

internal static class MetricRules
{
    /// <summary>指標名と値から品質を判定する。スクリーニング・比較で共用。</summary>
    public static Quality Evaluate(string metric, double v) => metric switch
    {
        "per" => v <= 0 ? Quality.Neutral : v <= 15 ? Quality.Good : v <= 25 ? Quality.Neutral : v <= 35 ? Quality.Caution : Quality.Bad,
        "pbr" => v <= 0 ? Quality.Neutral : v <= 1.0 ? Quality.Good : v <= 2.0 ? Quality.Neutral : v <= 3.5 ? Quality.Caution : Quality.Bad,
        "mix" => v <= 0 ? Quality.Neutral : v <= 10 ? Quality.Good : v <= 22.5 ? Quality.Neutral : v <= 40 ? Quality.Caution : Quality.Bad,
        "roe" => v >= 15 ? Quality.Good : v >= 10 ? Quality.Ok : v >= 5 ? Quality.Neutral : v >= 0 ? Quality.Caution : Quality.Bad,
        "dy" => v >= 7 ? Quality.Caution : v >= 4 ? Quality.Good : v >= 3 ? Quality.Ok : v >= 2 ? Quality.Neutral : Quality.Neutral,
        "benefityield" => v >= 2 ? Quality.Good : v >= 1 ? Quality.Ok : Quality.Neutral,
        "totalyield" => v >= 5 ? Quality.Good : v >= 4 ? Quality.Ok : v >= 2 ? Quality.Neutral : Quality.Neutral,
        "payout" => v <= 0 ? Quality.Neutral : v < 30 ? Quality.Neutral : v <= 60 ? Quality.Good : v <= 80 ? Quality.Caution : Quality.Bad,
        "equity" => v >= 60 ? Quality.Good : v >= 40 ? Quality.Ok : v >= 30 ? Quality.Neutral : v >= 20 ? Quality.Caution : Quality.Bad,
        "debt" => v <= 20 ? Quality.Good : v <= 40 ? Quality.Neutral : v <= 70 ? Quality.Caution : Quality.Bad,
        "growth" => v >= 10 ? Quality.Good : v >= 3 ? Quality.Ok : v >= 0 ? Quality.Neutral : Quality.Bad,
        "margin" => v >= 15 ? Quality.Good : v >= 8 ? Quality.Ok : v >= 3 ? Quality.Neutral : v >= 0 ? Quality.Caution : Quality.Bad,
        "cf" => v > 0 ? Quality.Ok : v == 0 ? Quality.Neutral : Quality.Bad,
        "cfmargin" => v >= 15 ? Quality.Good : v >= 8 ? Quality.Ok : v >= 0 ? Quality.Neutral : Quality.Bad,
        "score" => v >= 75 ? Quality.Good : v >= 60 ? Quality.Ok : v >= 45 ? Quality.Neutral : v >= 30 ? Quality.Caution : Quality.Bad,
        "buffett" => v >= 80 ? Quality.Good : v >= 65 ? Quality.Ok : v >= 50 ? Quality.Neutral : v >= 35 ? Quality.Caution : Quality.Bad,
        "change" => v >= 5 ? Quality.Ok : v >= -5 ? Quality.Neutral : Quality.Caution,
        _ => Quality.Neutral
    };
}

/// <summary>数値+メトリック名(ConverterParameter)からセル背景色を返す。</summary>
public class MetricBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = ToDouble(value);
        var metric = parameter?.ToString() ?? "";
        return Palette.Bg(MetricRules.Evaluate(metric, v));
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    internal static double ToDouble(object? value)
        => value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0
        };
}

/// <summary>スコア(0-100)から鮮やかな単色を返す(カード・バー用)。</summary>
public class ScoreToSolidBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = MetricBackgroundConverter.ToDouble(value);
        var metric = parameter?.ToString() == "buffett" ? "buffett" : "score";
        return Palette.Solid(MetricRules.Evaluate(metric, v));
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>ComparisonCell の背景色。</summary>
public class ComparisonCellBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ViewModels.ComparisonCell cell && cell.IsNumeric)
            return Palette.Bg(MetricRules.Evaluate(cell.Metric, cell.Value));
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool -> ○ / - フラグ表記。parameter="benefit" の場合 ◎/-。</summary>
public class BoolToFlagConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (parameter?.ToString() == "long") return b ? "◎" : "-";
        return b ? "○" : "-";
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool フラグの色(○=青系, -=グレー)。</summary>
public class FlagToBrushConverter : IValueConverter
{
    private static readonly Brush On = new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6));
    private static readonly Brush Off = new SolidColorBrush(Color.FromRgb(0x60, 0x6A, 0x72));

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? On : Off;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>ActiveScreen(string) が parameter と一致すれば true。上部ボタンのハイライト用。</summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>null/空 でない場合に Visible。</summary>
public class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool -> Visibility。</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (parameter?.ToString() == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>"#RRGGBB" 文字列 -> Brush。チャートのバー色に使用。</summary>
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrWhiteSpace(s)) return Brushes.Gray;
        try { return (Brush)new BrushConverter().ConvertFromString(s)!; }
        catch { return Brushes.Gray; }
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>YesNoUnknown -> 表示文字列。</summary>
public class YesNoUnknownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is YesNoUnknown y ? y switch
        {
            YesNoUnknown.Yes => "はい",
            YesNoUnknown.No => "いいえ",
            _ => "不明"
        } : "不明";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "はい" => YesNoUnknown.Yes,
            "いいえ" => YesNoUnknown.No,
            _ => YesNoUnknown.Unknown
        };
}
