using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // 自定义标题栏后，最大化要限制在显示器工作区，避免盖住任务栏
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (GetMonitorInfo(monitor, ref info))
        {
            var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            minMax.MaxPosition.X = info.WorkArea.Left - info.Monitor.Left;
            minMax.MaxPosition.Y = info.WorkArea.Top - info.Monitor.Top;
            minMax.MaxSize.X = info.WorkArea.Right - info.WorkArea.Left;
            minMax.MaxSize.Y = info.WorkArea.Bottom - info.WorkArea.Top;
            Marshal.StructureToPtr(minMax, lParam, true);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

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
