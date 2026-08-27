using System.Globalization;

namespace Plugin.Maui.OfflineSync.Sample;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : false;
}

public sealed class DoneDecorationsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TextDecorations.Strikethrough : TextDecorations.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
