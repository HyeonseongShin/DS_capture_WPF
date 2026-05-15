using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DSCapture.Helpers;
using DSCapture.Services;
using WPoint = System.Windows.Point;

namespace DSCapture.Views;

public partial class DragCaptureWindow : Window
{
    private readonly bool _useRatio169;
    private readonly Action<int, int, int, int> _onCapture;
    private readonly CaptureService _captureService = new();

    private Bitmap? _screenOriginal;
    private bool _isDragging;
    private WPoint _startPt;

    public DragCaptureWindow(bool useRatio169, Action<int, int, int, int> onCapture)
    {
        InitializeComponent();
        _useRatio169 = useRatio169;
        _onCapture = onCapture;

        Loaded += OnLoaded;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        // 1. 전체 화면 캡처
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        _screenOriginal = _captureService.CaptureScreen(
            screen.Left, screen.Top, screen.Width, screen.Height);

        // 2. 밝기 80% 감소 버전을 배경으로 설정
        using var dark = BitmapHelper.AdjustBrightness(_screenOriginal, 0.8f);
        var darkSrc = BitmapHelper.ToBitmapSource(dark);

        DarkBackground.Source = darkSrc;
        DarkBackground.Width  = screen.Width;
        DarkBackground.Height = screen.Height;

        // 3. 안내 텍스트 위치 (화면 상단 중앙)
        Canvas.SetLeft(HintBorder, (screen.Width - 640) / 2.0);
        Canvas.SetTop(HintBorder, 12);

        // 4. 십자선 초기 위치 (화면 밖)
        VLine.X1 = VLine.X2 = -10;
        VLine.Y1 = 0; VLine.Y2 = screen.Height;
        HLine.Y1 = HLine.Y2 = -10;
        HLine.X1 = 0; HLine.X2 = screen.Width;

        MainCanvas.Focus();
    }

    // ── 마우스 이벤트 ─────────────────────────────────────────
    private void OnMouseMove(object s, MouseEventArgs e)
    {
        var pos = e.GetPosition(MainCanvas);

        // 십자선 업데이트
        VLine.X1 = VLine.X2 = pos.X;
        HLine.Y1 = HLine.Y2 = pos.Y;

        if (!_isDragging) return;

        var (x0, y0, x1, y1) = CalcRect(_startPt, pos);
        double w = x1 - x0, h = y1 - y0;

        Canvas.SetLeft(SelectBox, x0); Canvas.SetTop(SelectBox, y0);
        SelectBox.Width = w; SelectBox.Height = h;
        SelectBox.Visibility = Visibility.Visible;

        // 크기 레이블
        SizeLabel.Text = $" {(int)w} x {(int)h} ";
        Canvas.SetLeft(SizeBg, x0);
        Canvas.SetTop(SizeBg, Math.Max(0, y0 - 24));
        SizeBg.Visibility = Visibility.Visible;

        // 원본(밝은) 미리보기
        if (_screenOriginal != null && w > 0 && h > 0)
        {
            try
            {
                using var crop = _screenOriginal.Clone(
                    new Rectangle((int)x0, (int)y0, (int)w, (int)h),
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                PreviewImage.Source = BitmapHelper.ToBitmapSource(crop);
                Canvas.SetLeft(PreviewImage, x0); Canvas.SetTop(PreviewImage, y0);
                PreviewImage.Width = w; PreviewImage.Height = h;
            }
            catch { }
        }
    }

    private void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _startPt = e.GetPosition(MainCanvas);
        MainCanvas.CaptureMouse();
    }

    private void OnMouseUp(object s, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        MainCanvas.ReleaseMouseCapture();

        var pos = e.GetPosition(MainCanvas);
        var (x0, y0, x1, y1) = CalcRect(_startPt, pos);

        Close();
        _onCapture((int)x0, (int)y0, (int)x1, (int)y1);
    }

    // ── 사각형 계산 (Shift/Ctrl 수식키 처리) ─────────────────
    private (double x0, double y0, double x1, double y1) CalcRect(WPoint start, WPoint current)
    {
        double sx = start.X, sy = start.Y;
        double dx = current.X - sx, dy = current.Y - sy;

        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        bool isCtrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);

        // Shift: 비율 고정
        if (isShift && Math.Abs(dx) > 0)
        {
            double ratio = _useRatio169 ? 16.0 / 9.0 : 4.0 / 3.0;
            double h = Math.Abs(dx) / ratio;
            dy = dy >= 0 ? h : -h;
        }

        double screenW = MainCanvas.ActualWidth;
        double screenH = MainCanvas.ActualHeight;

        if (isCtrl)
        {
            // Ctrl: 시작점 중앙 기준
            double maxDx = Math.Min(sx, screenW - sx);
            double maxDy = Math.Min(sy, screenH - sy);
            dx = Math.Sign(dx) * Math.Min(Math.Abs(dx), maxDx);
            dy = Math.Sign(dy) * Math.Min(Math.Abs(dy), maxDy);
            return (sx - Math.Abs(dx), sy - Math.Abs(dy),
                    sx + Math.Abs(dx), sy + Math.Abs(dy));
        }

        return (Math.Min(sx, sx + dx), Math.Min(sy, sy + dy),
                Math.Max(sx, sx + dx), Math.Max(sy, sy + dy));
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenOriginal?.Dispose();
        base.OnClosed(e);
    }
}
