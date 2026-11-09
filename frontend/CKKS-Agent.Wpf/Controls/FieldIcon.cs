using System.Windows;
using System.Windows.Media;

namespace CKKS_Agent.Wpf.Controls;

/// <summary>输入框左侧图标的矢量路径。</summary>
public static class FieldIcon
{
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.RegisterAttached(
        "Geometry",
        typeof(Geometry),
        typeof(FieldIcon),
        new FrameworkPropertyMetadata(null));

    public static Geometry? GetGeometry(DependencyObject element)
        => (Geometry?)element.GetValue(GeometryProperty);

    public static void SetGeometry(DependencyObject element, Geometry? value)
        => element.SetValue(GeometryProperty, value);
}
