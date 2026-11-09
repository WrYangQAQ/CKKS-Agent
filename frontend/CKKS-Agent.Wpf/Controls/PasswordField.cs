using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CKKS_Agent.Wpf.Controls;

/// <summary>带浮动标签和明文切换的密码输入框。</summary>
public sealed class PasswordField : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(PasswordField),
        new PropertyMetadata(""));

    public static readonly DependencyProperty HasTextProperty = DependencyProperty.Register(
        nameof(HasText),
        typeof(bool),
        typeof(PasswordField),
        new PropertyMetadata(false));

    private string _password = "";
    private PasswordBox? _maskedBox;
    private TextBox? _plainBox;
    private ToggleButton? _revealButton;
    private bool _syncing;

    public string Label
    {
        get => (string?)GetValue(LabelProperty) ?? "";
        set => SetValue(LabelProperty, value);
    }

    public bool HasText
    {
        get => (bool)GetValue(HasTextProperty);
        private set => SetValue(HasTextProperty, value);
    }

    public string Password => _password;

    public event EventHandler? PasswordChanged;

    public override void OnApplyTemplate()
    {
        if (_maskedBox is not null) _maskedBox.PasswordChanged -= OnMaskedPasswordChanged;
        if (_plainBox is not null) _plainBox.TextChanged -= OnPlainTextChanged;
        if (_revealButton is not null)
        {
            _revealButton.Checked -= OnRevealChanged;
            _revealButton.Unchecked -= OnRevealChanged;
        }

        base.OnApplyTemplate();

        _maskedBox = GetTemplateChild("PART_MaskedBox") as PasswordBox;
        _plainBox = GetTemplateChild("PART_PlainBox") as TextBox;
        _revealButton = GetTemplateChild("PART_RevealButton") as ToggleButton;

        if (_maskedBox is not null)
        {
            _maskedBox.PasswordChanged += OnMaskedPasswordChanged;
            _maskedBox.Password = _password;
        }

        if (_plainBox is not null)
        {
            _plainBox.TextChanged += OnPlainTextChanged;
            _plainBox.Text = _password;
        }

        if (_revealButton is not null)
        {
            _revealButton.Checked += OnRevealChanged;
            _revealButton.Unchecked += OnRevealChanged;
        }
    }

    private void OnMaskedPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        SetPassword(_maskedBox?.Password ?? "");
    }

    private void OnPlainTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        SetPassword(_plainBox?.Text ?? "");
    }

    private void SetPassword(string value)
    {
        if (string.Equals(_password, value, StringComparison.Ordinal)) return;

        _password = value;
        HasText = value.Length > 0;

        _syncing = true;
        if (_maskedBox is not null && _maskedBox.Password != value) _maskedBox.Password = value;
        if (_plainBox is not null && _plainBox.Text != value) _plainBox.Text = value;
        _syncing = false;

        PasswordChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        if (_maskedBox is null || _plainBox is null) return;

        var reveal = _revealButton?.IsChecked == true;

        _syncing = true;
        if (reveal) _plainBox.Text = _maskedBox.Password;
        else _maskedBox.Password = _plainBox.Text;
        _syncing = false;

        _maskedBox.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
        _plainBox.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;

        if (reveal)
        {
            _plainBox.Focus();
            _plainBox.CaretIndex = _plainBox.Text.Length;
        }
        else
        {
            _maskedBox.Focus();
        }
    }
}
