using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Drawing = System.Drawing;

namespace DSCapture.Services;

public class CaptureService
{
    /// <summary>
    /// 지정 영역을 캡처하여 클립보드에 복사하고 파일로 저장.
    /// Python execute_capture() 와 동일한 흐름.
    /// </summary>
    public string? CaptureAndSave(int x1, int y1, int x2, int y2,
        string saveDir, string saveFormat)
    {
        int l = Math.Min(x1, x2), t = Math.Min(y1, y2);
        int r = Math.Max(x1, x2), b = Math.Max(y1, y2);
        int w = r - l, h = b - t;
        if (w < 5 || h < 5) return null;

        try
        {
            using var bmp = CaptureScreen(l, t, w, h);

            // 클립보드 복사
            CopyToClipboard(bmp);

            // 파일 저장
            string fileName = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_capture.{saveFormat}";
            string filePath = Path.Combine(saveDir, fileName);
            Directory.CreateDirectory(saveDir);

            if (saveFormat == "jpg")
            {
                var jpegParams = new EncoderParameters(1);
                jpegParams.Param[0] = new EncoderParameter(
                    Encoder.Quality, 95L);
                var jpegCodec = GetEncoder(ImageFormat.Jpeg)!;
                bmp.Save(filePath, jpegCodec, jpegParams);
            }
            else
            {
                bmp.Save(filePath, ImageFormat.Png);
            }

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>전체 화면 캡처 (모든 모니터)</summary>
    public Bitmap CaptureFullScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        return CaptureScreen(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    /// <summary>지정 좌표의 화면 영역을 캡처하여 Bitmap 반환</summary>
    public Bitmap CaptureScreen(int x, int y, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new Drawing.Size(width, height),
            CopyPixelOperation.SourceCopy);
        return bmp;
    }

    /// <summary>Bitmap을 클립보드에 복사</summary>
    public void CopyToClipboard(Bitmap bitmap)
    {
        // WinForms Clipboard은 CF_DIB 자동 처리
        Clipboard.SetImage(bitmap);
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == format.Guid)
                return codec;
        return null;
    }
}
