using System.Globalization;
using System.Windows.Data;

namespace CKKS_Agent.Wpf.Converters;

/// <summary>把字符串属性与固定值比较，供单选按钮和字符串选项之间做双向绑定。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
