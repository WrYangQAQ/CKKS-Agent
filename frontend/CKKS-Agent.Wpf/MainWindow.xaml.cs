using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CKKS_Agent.Wpf.Controls;
using CKKS_Agent.Wpf.ViewModels;

namespace CKKS_Agent.Wpf;

public partial class MainWindow : Window
{
    private readonly AuthViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnLoginPasswordChanged(object? sender, EventArgs e)
        => _viewModel.LoginPassword = ((PasswordField)sender!).Password;

    private void OnRegisterPasswordChanged(object? sender, EventArgs e)
        => _viewModel.RegisterPassword = ((PasswordField)sender!).Password;

    // 点击输入框以外的空白区域时取消焦点，让输入框回到未选中状态。
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (e.OriginalSource is DependencyObject source && !IsInteractive(source))
        {
            Keyboard.ClearFocus();
        }
    }

    private static bool IsInteractive(DependencyObject source)
    {
        for (DependencyObject? node = source; node is not null; node = GetParent(node))
        {
            if (node is TextBoxBase or PasswordBox or ButtonBase or ComboBox)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject node)
        => node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
