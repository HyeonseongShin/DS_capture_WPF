using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using Drawing = System.Drawing;
using Interop = System.Windows.Interop;

namespace DSCapture.Helpers;

internal static class BitmapHelper
{
    /// <summary>GDI+ Bitmap을 WPF BitmapSource로 변환 (표시용)</summary>
    public static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    /// <summary>GDI+ Bitmap을 지정 scale로 축소하여 BitmapSource로 변환</summary>
    public static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap, double scale)
    {
        int dw = Math.Max(1, (int)(bitmap.Width * scale));
        int dh = Math.Max(1, (int)(bitmap.Height * scale));
        using var scaled = new Drawing.Bitmap(bitmap, dw, dh);
        return ToBitmapSource(scaled);
    }

    /// <summary>파일 경로에서 GDI+ Bitmap 로드 (파일 잠금 없이)</summary>
    public static Drawing.Bitmap LoadBitmap(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var temp = new Drawing.Bitmap(fs);
        var result = new Drawing.Bitmap(temp.Width, temp.Height, Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Drawing.Graphics.FromImage(result);
        g.DrawImage(temp, 0, 0, temp.Width, temp.Height);
        return result;
    }

    /// <summary>Bitmap 밝기 조정 (0.0 ~ 1.0, 1.0 = 원본)</summary>
    public static Drawing.Bitmap AdjustBrightness(Drawing.Bitmap source, float factor)
    {
        var result = new Drawing.Bitmap(source.Width, source.Height, source.PixelFormat);
        using var g = Drawing.Graphics.FromImage(result);
        var cm = new Drawing.Imaging.ColorMatrix(new float[][]
        {
            new[] { factor, 0, 0, 0, 0 },
            new[] { 0, factor, 0, 0, 0 },
            new[] { 0, 0, factor, 0, 0 },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f },
        });
        var ia = new Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm);
        g.DrawImage(source, new Drawing.Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height, Drawing.GraphicsUnit.Pixel, ia);
        return result;
    }
}
