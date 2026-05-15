using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DSCapture.Helpers;
using DSCapture.Models;
using DSCapture.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DSCapture.Views;

public partial class MainWindow : Window
{
    private const string AppVersion = "v1.00";

    private readonly AppSettings _settings;
    internal AppSettings AppSettings => _settings;
    private readonly SettingsService _settingsService;
    private readonly CaptureService _captureService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayService _trayService;

    private bool _useRatio169 = false;

    // 썸네일 데이터 모델
    private record ThumbnailItem(string Path, string Name, BitmapSource? Thumb);

    public MainWindow(LicenseData licData, bool startHidden = false)
    {
        InitializeComponent();

        Title = $"DS Capture {AppVersion} - [{licData.UserName}]";

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _captureService = new CaptureService();
        _hotkeyService = new HotkeyService();

        // 저장된 설정 복원
        TxtWidth.Text  = _settings.BoxWidth.ToString();
        TxtHeight.Text = _settings.BoxHeight.ToString();
        _useRatio169   = _settings.DragRatio == "16:9";
        UpdateRatioButtons();

        // 트레이 아이콘 초기화
        var icon = LoadTrayIcon();
        _trayService = new TrayService(icon,
            onOpen:       ShowMainWindow,
            onOpenFolder: OpenSaveFolder,
            onExit:       QuitApp);

        // 썸네일 렌더링
        UpdateThumbnails();

        Loaded += (_, _) =>
        {
            _hotkeyService.Initialize(this);
            ApplyShortcuts();
        };

        Closing += OnWindowClosing;

        if (startHidden) Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = System.Windows.PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == App.WM_SHOWME)
        {
            ShowMainWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── 단축키 적용 ───────────────────────────────────────────
    private void ApplyShortcuts()
    {
        _hotkeyService.ApplyShortcuts(_settings.Shortcuts,
            openFixed: () => Dispatcher.Invoke(OnFixedCapture_),
            openDrag:  () => Dispatcher.Invoke(OnDragCapture_),
            openFull:  () => Dispatcher.Invoke(OnFullCapture_));
    }

    // ── 캡처 버튼 핸들러 ─────────────────────────────────────
    private void OnFixedCapture(object s, RoutedEventArgs e) => OnFixedCapture_();
    private void OnFixedCapture_()
    {
        SaveSettings();
        Hide();
        int w = int.TryParse(TxtWidth.Text,  out var pw) ? pw : 800;
        int h = int.TryParse(TxtHeight.Text, out var ph) ? ph : 600;
        var box = new FixedBoxWindow(w, h, ExecuteCapture);
        box.Closed += (_, _) => ShowMainWindow();
        box.Show();
    }

    private void OnDragCapture(object s, RoutedEventArgs e) => OnDragCapture_();
    private void OnDragCapture_()
    {
        Hide();
        var drag = new DragCaptureWindow(_useRatio169, ExecuteCapture);
        drag.Closed += (_, _) => ShowMainWindow();
        drag.Show();
    }

    private void OnFullCapture(object s, RoutedEventArgs e) => OnFullCapture_();
    private void OnFullCapture_()
    {
        Hide();
        UpdateLayout();
        System.Threading.Thread.Sleep(300);
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        ExecuteCapture(screen.Left, screen.Top, screen.Right, screen.Bottom);
        ShowMainWindow();
    }

    // ── 캡처 실행 ────────────────────────────────────────────
    private void ExecuteCapture(int x1, int y1, int x2, int y2)
    {
        string? path = _captureService.CaptureAndSave(
            x1, y1, x2, y2, _settings.SaveDir, _settings.SaveFormat);

        if (path == null) return;

        _settings.RecentCaptures.Insert(0, path);
        if (_settings.RecentCaptures.Count > 10)
            _settings.RecentCaptures.RemoveAt(10);

        Dispatcher.InvokeAsync(UpdateThumbnails);
    }

    // ── 썸네일 목록 ──────────────────────────────────────────
    public void UpdateThumbnails()
    {
        var items = _settings.RecentCaptures
            .Where(File.Exists)
            .Select(path =>
            {
                BitmapSource? thumb = null;
                try
                {
                    using var bmp = BitmapHelper.LoadBitmap(path);
                    bmp.SetResolution(96, 96);
                    thumb = BitmapHelper.ToBitmapSource(bmp, Math.Min(160.0 / bmp.Width, 120.0 / bmp.Height));
                }
                catch { }
                return new ThumbnailItem(path, Path.GetFileName(path), thumb);
            })
            .ToList();

        ThumbnailList.ItemsSource = items;
    }

    private void OnThumbClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if (sender is Image img && img.Tag is string path && File.Exists(path))
            OpenEditor(path);
    }

    private void OnThumbRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image img || img.Tag is not string path) return;
        var menu = new ContextMenu();

        var editItem = new MenuItem { Header = "이미지 수정" };
        editItem.Click += (_, _) => OpenEditor(path);
        menu.Items.Add(editItem);

        menu.Items.Add(new Separator());

        var saveAsItem = new MenuItem { Header = "다른 이름으로 저장" };
        saveAsItem.Click += (_, _) => SaveThumbAs(path);
        menu.Items.Add(saveAsItem);

        var deleteItem = new MenuItem { Header = "삭제" };
        deleteItem.Click += (_, _) => DeleteThumb(path);
        menu.Items.Add(deleteItem);

        menu.IsOpen = true;
    }

    private void OpenEditor(string path)
    {
        var editor = new ImageEditorWindow(path, this);
        editor.Show();
    }

    private void SaveThumbAs(string srcPath)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|모든 파일|*.*",
            FileName = Path.GetFileName(srcPath),
            InitialDirectory = Path.GetDirectoryName(srcPath)
        };
        if (dlg.ShowDialog() == true)
            File.Copy(srcPath, dlg.FileName, overwrite: true);
    }

    private void DeleteThumb(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        _settings.RecentCaptures.Remove(path);
        UpdateThumbnails();
    }

    // ── 기타 버튼 ────────────────────────────────────────────
    private void OnClearAll(object s, RoutedEventArgs e)
    {
        if (_settings.RecentCaptures.Count == 0) return;
        if (MessageBox.Show("최근 캡처 목록의 모든 이미지를 삭제하시겠습니까?\n(실제 파일도 삭제됩니다)",
                "모두 지우기", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        foreach (var f in _settings.RecentCaptures.Where(File.Exists))
            try { File.Delete(f); } catch { }
        _settings.RecentCaptures.Clear();
        UpdateThumbnails();
    }

    private void OnSaveAllAs(object s, RoutedEventArgs e)
    {
        if (_settings.RecentCaptures.Count == 0) return;
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        foreach (var f in _settings.RecentCaptures.Where(File.Exists))
            File.Copy(f, Path.Combine(dlg.SelectedPath, Path.GetFileName(f)), overwrite: true);
    }

    private void OnSettings(object s, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings, _settingsService, ApplyShortcuts);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OnRatio43(object s, RoutedEventArgs e)  { _useRatio169 = false; UpdateRatioButtons(); }
    private void OnRatio169(object s, RoutedEventArgs e) { _useRatio169 = true;  UpdateRatioButtons(); }
    private void UpdateRatioButtons()
    {
        var active   = System.Windows.Media.Brushes.Teal;
        var inactive = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4b6584"));
        BtnRatio43.Background  = _useRatio169 ? inactive : active;
        BtnRatio169.Background = _useRatio169 ? active   : inactive;
    }

    // ── 창 / 트레이 관리 ─────────────────────────────────────
    private void ShowMainWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void OpenSaveFolder()
    {
        if (Directory.Exists(_settings.SaveDir))
            Process.Start("explorer.exe", _settings.SaveDir);
    }

    private void QuitApp()
    {
        SaveSettings();
        _hotkeyService.Dispose();
        _trayService.Dispose();
        Application.Current.Shutdown();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        if (_settings.CloseAction == "tray")
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            QuitApp();
        }
    }

    private void SaveSettings()
    {
        _settings.BoxWidth   = int.TryParse(TxtWidth.Text,  out var w) ? w : 800;
        _settings.BoxHeight  = int.TryParse(TxtHeight.Text, out var h) ? h : 600;
        _settings.DragRatio  = _useRatio169 ? "16:9" : "4:3";
        _settingsService.Save(_settings);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/DS_capture.ico");
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo != null)
                return new System.Drawing.Icon(streamInfo.Stream);
        }
        catch { }

        // 아이콘이 없으면 단색 아이콘 동적 생성
        using var bmp = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(47, 53, 66));
        g.DrawRectangle(new System.Drawing.Pen(System.Drawing.Color.Teal, 2), 3, 3, 10, 10);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
}
