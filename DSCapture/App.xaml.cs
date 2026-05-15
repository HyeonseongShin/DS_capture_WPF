using System;
using System.Text;
using System.Threading;
using System.Windows;
using DSCapture.Helpers;
using DSCapture.Services;
using DSCapture.Views;

namespace DSCapture;

public partial class App : Application
{
    private static Mutex? _mutex;
    public static readonly int WM_SHOWME = NativeMethods.RegisterWindowMessage("WM_SHOWME_DSCAPTURE");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // DPI 인식 설정
        try { NativeMethods.SetProcessDpiAwareness(1); } catch { }

        // 단일 인스턴스 체크
        _mutex = new Mutex(true, "Global\\DSCapture_Unique_Instance_Mutex", out bool isNew);
        if (!isNew)
        {
            FocusExistingWindow();
            Shutdown();
            return;
        }

        // 라이센스 검증
        var hwidService   = new HwidService();
        var licenseService = new LicenseService(hwidService);
        var (isValid, licData) = licenseService.CheckLicense("DS_CAPTURE");

        if (!isValid)
        {
            Shutdown();
            return;
        }

        bool isStartup = Array.Exists(e.Args, a => a == "--startup");

        var mainWindow = new MainWindow(licData!, isStartup);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void FocusExistingWindow()
    {
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            var title = new StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, title, 256);
            if (title.ToString().StartsWith("DS Capture"))
            {
                NativeMethods.PostMessage(hwnd, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                return false;
            }
            return true;
        }, IntPtr.Zero);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
