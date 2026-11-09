using System.Windows;

namespace CKKS_Agent.Wpf.Controls;

/// <summary>浮动标签文案，挂在 TextBox 等输入控件上。</summary>
public static class FloatingLabel
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(FloatingLabel),
        new FrameworkPropertyMetadata(""));

    public static string GetText(DependencyObject element)
        => (string?)element.GetValue(TextProperty) ?? "";

    public static void SetText(DependencyObject element, string value)
        => element.SetValue(TextProperty, value);
}
