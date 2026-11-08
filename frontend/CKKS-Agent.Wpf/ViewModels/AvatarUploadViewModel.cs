using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CKKS_Agent.Wpf.Services;
using Microsoft.Win32;

namespace CKKS_Agent.Wpf.ViewModels;

public sealed class AvatarUploadViewModel : INotifyPropertyChanged
{
    private readonly AvatarUploadService _uploadService = new();
    private string? _selectedPath;
    private ImageSource? _avatarPreview;
    private string _statusMessage = "";
    private Brush _statusBrush = Brushes.SlateGray;
    private bool _isUploading;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ImageSource? AvatarPreview { get => _avatarPreview; private set { _avatarPreview = value; OnChanged(); OnChanged(nameof(AvatarPlaceholderVisibility)); } }
    public Visibility AvatarPlaceholderVisibility => AvatarPreview is null ? Visibility.Visible : Visibility.Collapsed;
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnChanged(); } }
    public Brush StatusBrush { get => _statusBrush; private set { _statusBrush = value; OnChanged(); } }
    public bool CanUpload => _selectedPath is not null && !_isUploading;
    public ICommand SelectFileCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand UploadCommand { get; }

    public AvatarUploadViewModel()
    {
        SelectFileCommand = new AsyncCommand(SelectFileAsync);
        ResetCommand = new RelayCommand(Reset);
        UploadCommand = new AsyncCommand(UploadAsync, () => CanUpload);
    }

    public async Task SelectFileAsync()
    {
        var dialog = new OpenFileDialog { Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp", Multiselect = false, CheckFileExists = true };
        if (dialog.ShowDialog() == true) await SelectFileAsync(dialog.FileName);
    }

    public async Task SelectFileAsync(string path)
    {
        try
        {
            await _uploadService.ValidateAsync(path);
            AvatarPreview = LoadPreview(path);
            _selectedPath = path;
            StatusMessage = "图片已准备好，确认无误后点击“保存头像”。";
            StatusBrush = Brushes.RoyalBlue;
        }
        catch (AvatarValidationException exception)
        {
            StatusMessage = exception.Message;
            StatusBrush = Brushes.Firebrick;
        }
        finally { NotifyUploadState(); }
    }

    private async Task UploadAsync()
    {
        if (_selectedPath is null) return;
        _isUploading = true; NotifyUploadState();
        StatusMessage = "正在安全上传头像…"; StatusBrush = Brushes.RoyalBlue;
        try
        {
            var avatarUrl = await _uploadService.UploadAsync(_selectedPath, AuthSession.AccessToken);
            StatusMessage = $"头像已保存：{avatarUrl}";
            StatusBrush = Brushes.SeaGreen;
            _selectedPath = null;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            StatusBrush = Brushes.Firebrick;
        }
        finally { _isUploading = false; NotifyUploadState(); }
    }

    private void Reset() { _selectedPath = null; AvatarPreview = null; StatusMessage = ""; NotifyUploadState(); }
    private static BitmapImage LoadPreview(string path) { var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri(path); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 512; image.EndInit(); image.Freeze(); return image; }
    private void NotifyUploadState() { OnChanged(nameof(CanUpload)); (UploadCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
