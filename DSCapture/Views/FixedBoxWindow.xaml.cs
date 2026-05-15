using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DSCapture.Views;

public partial class FixedBoxWindow : Window
{
    private const int ControlBarH = 40;
    private const int ResizeMargin = 15;
    private const int MinSize = 150;

    private readonly Action<int, int, int, int> _onCapture;

    private bool _isCapturing;
    private bool _barAtBottom;

    // 리사이즈/이동 상태
    private enum DragMode { None, Move, Resize }
    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private Rect _windowRect;

    public FixedBoxWindow(int width, int height, Action<int, int, int, int> onCapture)
    {
        InitializeComponent();
        _onCapture = onCapture;

        Width  = width;
        Height = height + ControlBarH;

        UpdateSizeLabel();

        // 이벤트 바인딩
        HitArea.MouseMove        += OnMouseMove;
        HitArea.MouseLeftButtonDown  += OnMouseDown;
        HitArea.MouseLeftButtonUp    += OnMouseUp;
        TopBar.MouseLeftButtonDown   += OnBarMouseDown;
        PreviewKeyDown += OnKeyDown;

        LocationChanged += (_, _) => UpdateBarPosition();
        SizeChanged     += (_, _) => UpdateSizeLabel();
    }

    // ── 키 처리 ──────────────────────────────────────────────
    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Return)  { DoCapture(); e.Handled = true; }
        if (e.Key == Key.Escape)  { Close(); }
    }

    private void OnClose(object s, RoutedEventArgs e) => Close();
    private void OnCapture(object s, RoutedEventArgs e) => DoCapture();

    // ── 리사이즈/이동 ─────────────────────────────────────────
    private void OnMouseMove(object s, MouseEventArgs e)
    {
        if (_dragMode != DragMode.None)
        {
            PerformDrag(e.GetPosition(null));
            return;
        }

        // 커서 변경
        Cursor = GetResizeCursor(e.GetPosition(HitArea));
    }

    private void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(HitArea);
        var cursor = GetResizeCursor(pos);

        if (cursor == Cursors.SizeAll)
        {
            _dragMode = DragMode.Move;
        }
        else
        {
            _dragMode = DragMode.Resize;
            _resizeCursor = cursor;
        }

        _dragStart  = e.GetPosition(null);
        _windowRect = new Rect(Left, Top, Width, Height);
        HitArea.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseUp(object s, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
        HitArea.ReleaseMouseCapture();
    }

    private void OnBarMouseDown(object s, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private Cursor _resizeCursor = Cursors.Arrow;

    private Cursor GetResizeCursor(Point p)
    {
        double w = HitArea.ActualWidth, h = HitArea.ActualHeight;
        bool l = p.X < ResizeMargin, r = p.X > w - ResizeMargin;
        bool t = p.Y < ResizeMargin, b = p.Y > h - ResizeMargin;

        if ((l && t) || (r && b)) return Cursors.SizeNWSE;
        if ((r && t) || (l && b)) return Cursors.SizeNESW;
        if (l || r) return Cursors.SizeWE;
        if (t || b) return Cursors.SizeNS;
        return Cursors.SizeAll;
    }

    private void PerformDrag(Point current)
    {
        double dx = current.X - _dragStart.X;
        double dy = current.Y - _dragStart.Y;

        if (_dragMode == DragMode.Move)
        {
            Left = _windowRect.Left + dx;
            Top  = _windowRect.Top  + dy;
            return;
        }

        // Resize: 핵심 커서 방향에 따라 크기/위치 조정
        double newW = _windowRect.Width;
        double newH = _windowRect.Height;
        double newL = _windowRect.Left;
        double newT = _windowRect.Top;

        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (_resizeCursor == Cursors.SizeWE || _resizeCursor == Cursors.SizeNWSE || _resizeCursor == Cursors.SizeNESW)
            newW = Math.Max(MinSize, _windowRect.Width + dx);

        if (_resizeCursor == Cursors.SizeNS || _resizeCursor == Cursors.SizeNWSE || _resizeCursor == Cursors.SizeNESW)
        {
            if (isShift)
            {
                double ratio = _windowRect.Width / (_windowRect.Height - ControlBarH);
                newH = newW / ratio + ControlBarH;
            }
            else
            {
                newH = Math.Max(MinSize + ControlBarH, _windowRect.Height + dy);
            }
        }

        Width  = newW;
        Height = newH;
        Left   = newL;
        Top    = newT;
    }

    // ── 컨트롤 바 자동 전환 ───────────────────────────────────
    private void UpdateBarPosition()
    {
        bool shouldBeBottom = Top < ControlBarH;
        if (shouldBeBottom == _barAtBottom) return;

        _barAtBottom = shouldBeBottom;
        if (_barAtBottom)
        {
            TopBar.VerticalAlignment    = VerticalAlignment.Bottom;
            CaptureBorder.Margin = new Thickness(0, 0, 0, ControlBarH);
        }
        else
        {
            TopBar.VerticalAlignment    = VerticalAlignment.Top;
            CaptureBorder.Margin = new Thickness(0, ControlBarH, 0, 0);
        }
    }

    private void UpdateSizeLabel()
    {
        int w = (int)Width;
        int h = (int)(Height - ControlBarH);
        SizeLabel.Text = $"{w} x {h}";
    }

    // ── 캡처 실행 ────────────────────────────────────────────
    private void DoCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        int x = (int)Left;
        int y = _barAtBottom ? (int)Top : (int)(Top + ControlBarH);
        int w = (int)Width;
        int h = (int)(Height - ControlBarH);

        Hide();
        Thread.Sleep(200);

        try { _onCapture(x, y, x + w, y + h); }
        finally
        {
            Show();
            Activate();
            new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal,
                (_, _) => _isCapturing = false, Dispatcher) { IsEnabled = true };
        }
    }
}
