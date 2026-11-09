using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CKKS_Agent.Wpf.Converters;

/// <summary>按登录方式 / 验证方式返回输入框左侧图标。</summary>
public sealed class FieldIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "PhoneNumber" => "IconPhone",
            "Email" => "IconMail",
            _ => "IconUser"
        };

        return Application.Current?.TryFindResource(key) as Geometry;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
