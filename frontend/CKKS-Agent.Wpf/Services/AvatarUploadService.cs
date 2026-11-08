using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CKKS_Agent.Wpf.Services;

public sealed class AvatarValidationException(string message) : Exception(message);

public sealed class AvatarUploadService
{
    private const long MaxFileSize = 2 * 1024 * 1024;
    // 联调时改成你的 HTTPS API 地址；正式环境由统一配置注入。
    private static readonly Uri UploadUri = new("https://localhost:7206/api/user/avatar");

    public async Task ValidateAsync(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length == 0) throw new AvatarValidationException("请选择有效的图片文件。");
        if (file.Length > MaxFileSize) throw new AvatarValidationException("图片不能超过 2 MB。");

        await using var stream = File.OpenRead(path);
        var header = new byte[12];
        var read = await stream.ReadAsync(header);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var isWebp = read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!isJpeg && !isPng && !isWebp) throw new AvatarValidationException("文件内容不是支持的 JPG、PNG 或 WebP 图片。");

        try { _ = LoadImageWithoutLock(path); }
        catch { throw new AvatarValidationException("图片已损坏或无法读取。"); }
    }

    public async Task<string> UploadAsync(string path, string? accessToken)
    {
        await ValidateAsync(path);
        using var client = new HttpClient();
        if (!string.IsNullOrWhiteSpace(accessToken)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await using var stream = File.OpenRead(path);
        using var form = new MultipartFormDataContent();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(GetMediaType(path));
        form.Add(content, "avatar", Path.GetFileName(path));
        using var response = await client.PostAsync(UploadUri, form);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"上传失败：{response.StatusCode}");
        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean()) throw new InvalidOperationException("服务器拒绝了头像文件。");
        return json.RootElement.GetProperty("data").GetProperty("avatarUrl").GetString() ?? "";
    }

    private static string GetMediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => "application/octet-stream" };
    private static System.Windows.Media.Imaging.BitmapImage LoadImageWithoutLock(string path) { var image = new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.UriSource = new Uri(path); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.EndInit(); image.Freeze(); return image; }
}
