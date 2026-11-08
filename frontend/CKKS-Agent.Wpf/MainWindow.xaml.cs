using System.Windows;
using System.Windows.Input;
using CKKS_Agent.Wpf.ViewModels;

namespace CKKS_Agent.Wpf;

public partial class MainWindow : Window
{
    private AvatarUploadViewModel ViewModel => (AvatarUploadViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new AvatarUploadViewModel();
    }

    private void UploadDropZone_DragEnter(object sender, DragEventArgs e) => e.Effects =
        e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void UploadDropZone_DragLeave(object sender, DragEventArgs e) => e.Effects = DragDropEffects.None;

    private async void UploadDropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            await ViewModel.SelectFileAsync(files[0]);
    }

    private async void UploadDropZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => await ViewModel.SelectFileAsync();
}
