using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;

namespace CKKS_Agent_System.Helper
{
    public static class ImageValidatorHelper
    {
        private const int MaxWidth = 4000; // 最大宽度
        private const int MaxHeight = 4000; // 最大高度

        // 安全验证并重新编码图片
        public static async Task<bool> ValidateAndReencodeAsync(Stream fileStream, Stream outputStream, string extension)
        {
            try
            {
                // 如果文件是伪造的（比如改了后缀的脚本），这里会抛出异常
                using var image = await Image.LoadAsync(fileStream);

                // 防止用户上传超大图片占用内存
                if (image.Width > MaxWidth || image.Height > MaxHeight)
                {
                    return false;
                }

                // 获取图片的真实格式
                var format = image.Metadata.DecodedImageFormat;

                if (format is JpegFormat && extension is ".jpg" or ".jpeg")
                {
                    // 重编码为 JPEG，设置质量 85（兼顾体积和清晰度），不保存原图的 EXIF 信息，直接剥离，防止恶意元数据注入
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 85, SkipMetadata = true });
                }
                else if (format is PngFormat && extension == ".png")
                {
                    // 重编码为 PNG，使用默认压缩
                    await image.SaveAsPngAsync(outputStream, new PngEncoder { SkipMetadata = true });
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
