using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DSCapture.Helpers;
using Microsoft.Win32;
using WColor = System.Windows.Media.Color;
using WPoint = System.Windows.Point;

namespace DSCapture.Views;

public partial class ImageEditorWindow : Window
{
    private const int MaxUndo = 10;

    private readonly MainWindow _mainApp;
    private string _filepath;

    // GDI+ 이미지 상태
    private Bitmap _editBitmap;
    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();

    // 도구 상태
    private string _currentTool = "pen";
    private System.Drawing.Color _drawColor  = System.Drawing.Color.Red;
    private System.Drawing.Color _fillColor  = System.Drawing.Color.Red;
    private bool _useFill = false;
    private string _fontFamily = "Malgun Gothic";

    // 드로잉 상태
    private double _scale = 1.0;
    private WPoint _startPt;
    private List<WPoint> _penPoints = new();
    private UIElement? _previewShape;

    private static readonly string[] PaletteColors =
        { "#FF0000","#FF8C00","#FFFF00","#008000","#0000FF","#800080","#FFFFFF","#000000" };

    public ImageEditorWindow(string filepath, MainWindow mainApp)
    {
        InitializeComponent();
        _filepath = filepath;
        _mainApp  = mainApp;

        Title = $"편집기 — {Path.GetFileName(filepath)}";

        _editBitmap = BitmapHelper.LoadBitmap(filepath);

        // 팔레트 초기화
        ColorPalette.ItemsSource     = PaletteColors.Select(c => new SolidColorBrush(ParseWColor(c))).ToArray();
        FillColorPalette.ItemsSource = PaletteColors.Select(c => new SolidColorBrush(ParseWColor(c))).ToArray();
        FontFamilyBox.SelectedIndex = 0;

        // 단축키
        KeyDown += OnWindowKeyDown;

        Loaded += (_, _) => FitAndRefresh();
        SizeChanged += (_, _) => FitAndRefresh();

        // 첫 번째 도구 선택
        BtnPen.IsChecked = true;
    }

    // ══ 도구 선택 ════════════════════════════════════════════
    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;
        _currentTool = tb.Tag?.ToString() ?? "pen";

        foreach (var btn in new[] { BtnPen, BtnLine, BtnArrow, BtnRect, BtnEllipse,
                                    BtnText, BtnHighlight, BtnMosaic, BtnCrop })
            if (btn != tb) btn.IsChecked = false;
    }

    // ══ 색상 ════════════════════════════════════════════════
    private void OnPaletteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SolidColorBrush b)
            SetDrawColor(WColorToGdi(b.Color));
    }

    private void OnFillPaletteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SolidColorBrush b)
        {
            _fillColor = WColorToGdi(b.Color);
            FillColorIndicator.Background = new SolidColorBrush(b.Color);
            FillCheck.IsChecked = true;
            _useFill = true;
        }
    }

    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.ColorDialog { Color = _drawColor };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SetDrawColor(dlg.Color);
    }

    private void OnPickFillColor(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.ColorDialog { Color = _fillColor };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _fillColor = dlg.Color;
            FillColorIndicator.Background = new SolidColorBrush(GdiToWColor(dlg.Color));
            FillCheck.IsChecked = true;
            _useFill = true;
        }
    }

    private void SetDrawColor(System.Drawing.Color color)
    {
        _drawColor = color;
        ColorIndicator.Background = new SolidColorBrush(GdiToWColor(color));
    }

    // ══ 캔버스 마우스 이벤트 ════════════════════════════════
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPt = e.GetPosition(DrawingCanvas);
        _penPoints = new List<WPoint> { _startPt };
        RemovePreview();
        DrawingCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(DrawingCanvas);
        _penPoints.Add(pos);

        RemovePreview();
        _previewShape = CreatePreviewShape(_startPt, pos);
        if (_previewShape != null)
            DrawingCanvas.Children.Add(_previewShape);
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        DrawingCanvas.ReleaseMouseCapture();
        RemovePreview();

        var pos = e.GetPosition(DrawingCanvas);
        var (ix, iy)   = C2I(pos.X, pos.Y);
        var (isx, isy) = C2I(_startPt.X, _startPt.Y);

        if (_currentTool == "text")
        {
            DoText(isx, isy);
            return;
        }
        if (_currentTool == "crop")
        {
            DoCrop(isx, isy, ix, iy);
            return;
        }

        PushUndo();
        DrawOnBitmap(isx, isy, ix, iy);
        RefreshCanvas();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0) OnUndo(sender, e);
        if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0) OnRedo(sender, e);
    }

    // ══ 프리뷰 (WPF 임시 도형) ══════════════════════════════
    private UIElement? CreatePreviewShape(WPoint s, WPoint e)
    {
        double x0 = Math.Min(s.X, e.X), y0 = Math.Min(s.Y, e.Y);
        double x1 = Math.Max(s.X, e.X), y1 = Math.Max(s.Y, e.Y);
        var stroke = new SolidColorBrush(GdiToWColor(_drawColor));
        int lw = GetLineWidth();

        switch (_currentTool)
        {
            case "pen":
                var poly = new System.Windows.Shapes.Polyline
                {
                    Stroke = stroke, StrokeThickness = lw,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                foreach (var p in _penPoints) poly.Points.Add(p);
                return poly;

            case "line":
                return new System.Windows.Shapes.Line
                { X1=s.X,Y1=s.Y,X2=e.X,Y2=e.Y, Stroke=stroke, StrokeThickness=lw };

            case "arrow":
                return new System.Windows.Shapes.Line
                { X1=s.X,Y1=s.Y,X2=e.X,Y2=e.Y, Stroke=stroke, StrokeThickness=lw };

            case "rect":
                var r = new System.Windows.Shapes.Rectangle
                { Width=x1-x0, Height=y1-y0, Stroke=stroke, StrokeThickness=lw,
                  Fill = _useFill ? new SolidColorBrush(GdiToWColor(_fillColor)) : System.Windows.Media.Brushes.Transparent };
                Canvas.SetLeft(r, x0); Canvas.SetTop(r, y0);
                return r;

            case "ellipse":
                var el = new System.Windows.Shapes.Ellipse
                { Width=x1-x0, Height=y1-y0, Stroke=stroke, StrokeThickness=lw,
                  Fill = _useFill ? new SolidColorBrush(GdiToWColor(_fillColor)) : System.Windows.Media.Brushes.Transparent };
                Canvas.SetLeft(el, x0); Canvas.SetTop(el, y0);
                return el;

            case "highlight":
                var hl = new System.Windows.Shapes.Rectangle
                { Width=x1-x0, Height=y1-y0,
                  Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100,
                      _drawColor.R, _drawColor.G, _drawColor.B)) };
                Canvas.SetLeft(hl, x0); Canvas.SetTop(hl, y0);
                return hl;

            case "mosaic": case "crop": case "text":
                var sel = new System.Windows.Shapes.Rectangle
                { Width=x1-x0, Height=y1-y0, Stroke=System.Windows.Media.Brushes.LimeGreen,
                  StrokeThickness=2, StrokeDashArray=new DoubleCollection{6,4} };
                Canvas.SetLeft(sel, x0); Canvas.SetTop(sel, y0);
                return sel;

            default: return null;
        }
    }

    private void RemovePreview()
    {
        if (_previewShape != null)
        {
            DrawingCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }
    }

    // ══ GDI+ 실제 드로잉 ════════════════════════════════════
    private void DrawOnBitmap(int sx, int sy, int ex, int ey)
    {
        using var g = Graphics.FromImage(_editBitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;

        var color = System.Drawing.Color.FromArgb(255, _drawColor.R, _drawColor.G, _drawColor.B);
        int lw = GetLineWidth();
        using var pen = new System.Drawing.Pen(color, lw)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };

        int x0 = Math.Min(sx, ex), y0 = Math.Min(sy, ey);
        int x1 = Math.Max(sx, ex), y1 = Math.Max(sy, ey);

        switch (_currentTool)
        {
            case "pen":
                var pts = _penPoints.Select(p => C2I(p.X, p.Y))
                    .Select(t => new PointF(t.x, t.y)).ToArray();
                if (pts.Length >= 2)
                    g.DrawLines(pen, pts);
                else if (pts.Length == 1)
                {
                    int r2 = Math.Max(1, lw / 2);
                    g.FillEllipse(new SolidBrush(color), pts[0].X-r2, pts[0].Y-r2, r2*2, r2*2);
                }
                break;

            case "line":
                g.DrawLine(pen, sx, sy, ex, ey);
                break;

            case "arrow":
                g.DrawLine(pen, sx, sy, ex, ey);
                DrawArrowHead(g, sx, sy, ex, ey, color, lw);
                break;

            case "rect":
                if (_useFill)
                    g.FillRectangle(new SolidBrush(_fillColor), x0, y0, x1-x0, y1-y0);
                g.DrawRectangle(pen, x0, y0, x1-x0, y1-y0);
                break;

            case "ellipse":
                if (_useFill)
                    g.FillEllipse(new SolidBrush(_fillColor), x0, y0, x1-x0, y1-y0);
                g.DrawEllipse(pen, x0, y0, x1-x0, y1-y0);
                break;

            case "highlight":
                g.FillRectangle(new SolidBrush(System.Drawing.Color.FromArgb(100,
                    _drawColor.R, _drawColor.G, _drawColor.B)), x0, y0, x1-x0, y1-y0);
                break;

            case "mosaic":
                ApplyMosaic(x0, y0, x1-x0, y1-y0);
                break;
        }
    }

    private static void DrawArrowHead(Graphics g, int x1, int y1, int x2, int y2,
        System.Drawing.Color color, int lw)
    {
        double angle = Math.Atan2(y2 - y1, x2 - x1);
        double size  = Math.Max(12, lw * 4);
        double spread = Math.PI / 6;

        using var pen = new System.Drawing.Pen(color, lw) { EndCap = LineCap.Round };
        for (int i = -1; i <= 1; i += 2)
        {
            double side = spread * i;
            int ex = (int)(x2 - size * Math.Cos(angle - side));
            int ey = (int)(y2 - size * Math.Sin(angle - side));
            g.DrawLine(pen, x2, y2, ex, ey);
        }
    }

    private void ApplyMosaic(int x0, int y0, int w, int h)
    {
        if (w <= 4 || h <= 4) return;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
        w = Math.Min(w, _editBitmap.Width  - x0);
        h = Math.Min(h, _editBitmap.Height - y0);
        if (w <= 0 || h <= 0) return;

        var region = _editBitmap.Clone(new Rectangle(x0, y0, w, h), _editBitmap.PixelFormat);
        var small  = new Bitmap(Math.Max(1, w / 10), Math.Max(1, h / 10));
        using (var gs = Graphics.FromImage(small))
        {
            gs.InterpolationMode = InterpolationMode.NearestNeighbor;
            gs.DrawImage(region, 0, 0, small.Width, small.Height);
        }
        var pixelated = new Bitmap(w, h);
        using (var gp = Graphics.FromImage(pixelated))
        {
            gp.InterpolationMode = InterpolationMode.NearestNeighbor;
            gp.PixelOffsetMode = PixelOffsetMode.Half;
            gp.DrawImage(small, 0, 0, w, h);
        }
        using var gm = Graphics.FromImage(_editBitmap);
        gm.DrawImage(pixelated, x0, y0);
    }

    private void DoText(int x, int y)
    {
        var dlg = new TextInputDialog { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.InputText)) return;
        string text = dlg.InputText;

        PushUndo();
        using var g = Graphics.FromImage(_editBitmap);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var font  = new Font(_fontFamily, GetFontSize());
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(255, _drawColor));
        g.DrawString(text, font, brush, x, y);
        RefreshCanvas();
    }

    private void DoCrop(int sx, int sy, int ex, int ey)
    {
        int x0 = Math.Max(0, Math.Min(sx, ex));
        int y0 = Math.Max(0, Math.Min(sy, ey));
        int x1 = Math.Min(_editBitmap.Width,  Math.Max(sx, ex));
        int y1 = Math.Min(_editBitmap.Height, Math.Max(sy, ey));
        if (x1 - x0 < 5 || y1 - y0 < 5) return;

        PushUndo();
        _editBitmap = _editBitmap.Clone(new Rectangle(x0, y0, x1-x0, y1-y0), _editBitmap.PixelFormat);
        FitAndRefresh();
    }

    // ══ Undo / Redo ══════════════════════════════════════════
    private void PushUndo()
    {
        _undoStack.Push((Bitmap)_editBitmap.Clone());
        if (_undoStack.Count > MaxUndo) _undoStack.TryPop(out _);
        _redoStack.Clear();
    }

    private void OnUndo(object s, RoutedEventArgs e)
    {
        if (!_undoStack.TryPop(out var prev)) return;
        _redoStack.Push((Bitmap)_editBitmap.Clone());
        _editBitmap = prev;
        FitAndRefresh();
    }

    private void OnRedo(object s, RoutedEventArgs e)
    {
        if (!_redoStack.TryPop(out var next)) return;
        _undoStack.Push((Bitmap)_editBitmap.Clone());
        _editBitmap = next;
        FitAndRefresh();
    }

    // ══ 변환 ═════════════════════════════════════════════════
    private void OnRotateCCW(object s, RoutedEventArgs e)
    {
        PushUndo();
        _editBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
        FitAndRefresh();
    }

    private void OnRotateCW(object s, RoutedEventArgs e)
    {
        PushUndo();
        _editBitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
        FitAndRefresh();
    }

    private void OnFlipH(object s, RoutedEventArgs e)
    {
        PushUndo();
        _editBitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
        FitAndRefresh();
    }

    private void OnFlipV(object s, RoutedEventArgs e)
    {
        PushUndo();
        _editBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
        FitAndRefresh();
    }

    // ══ 저장 / 클립보드 ══════════════════════════════════════
    private void OnSave(object s, RoutedEventArgs e)
    {
        SaveToPath(_filepath);
        Title = $"편집기 — {Path.GetFileName(_filepath)} ✓";
        _mainApp.UpdateThumbnails();
    }

    private void OnSaveAs(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|모든 파일|*.*",
            DefaultExt = ".png",
            InitialDirectory = _mainApp.AppSettings.SaveDir
        };
        if (dlg.ShowDialog() != true) return;
        SaveToPath(dlg.FileName);
        _filepath = dlg.FileName;
        Title = $"편집기 — {Path.GetFileName(_filepath)} ✓";
    }

    private void SaveToPath(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        if (ext is ".jpg" or ".jpeg")
        {
            var codec = GetJpegCodec();
            var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
            _editBitmap.Save(path, codec!, p);
        }
        else
        {
            _editBitmap.Save(path, ImageFormat.Png);
        }
    }

    private void OnCopyClipboard(object s, RoutedEventArgs e)
        => System.Windows.Forms.Clipboard.SetImage(_editBitmap);

    // ══ 캔버스 표시 ══════════════════════════════════════════
    private void FitAndRefresh()
    {
        double cw = Math.Max(DrawingCanvas.ActualWidth,  400);
        double ch = Math.Max(DrawingCanvas.ActualHeight, 300);
        double iw = _editBitmap.Width, ih = _editBitmap.Height;
        _scale = Math.Min(1.0, Math.Min(cw / iw, ch / ih));

        RefreshCanvas();
        SizeLabel.Text = $"{iw} × {ih} px  ({_scale * 100:F0}%)";
    }

    public void UpdateThumbnails() => _mainApp.UpdateThumbnails();

    private void RefreshCanvas()
    {
        DisplayImage.Source = BitmapHelper.ToBitmapSource(_editBitmap, _scale);
    }

    private (int x, int y) C2I(double cx, double cy)
        => ((int)(cx / _scale), (int)(cy / _scale));

    private int GetLineWidth()
        => int.TryParse(LineWidthBox.Text, out int w) ? Math.Max(1, w) : 5;

    private int GetFontSize()
        => int.TryParse(FontSizeBox.Text, out int s) ? Math.Max(8, s) : 20;

    // ══ 헬퍼 ═════════════════════════════════════════════════
    private static WColor ParseWColor(string hex)
        => (WColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    private static System.Drawing.Color WColorToGdi(WColor c)
        => System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

    private static WColor GdiToWColor(System.Drawing.Color c)
        => WColor.FromArgb(c.A, c.R, c.G, c.B);

    private static ImageCodecInfo? GetJpegCodec()
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == ImageFormat.Jpeg.Guid)
                return codec;
        return null;
    }
}
