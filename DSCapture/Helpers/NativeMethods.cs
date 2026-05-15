using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DSCapture.Helpers;

internal static class NativeMethods
{
    // 창 열거 / 활성화
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


    // 전역 단축키
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    // GDI 핸들 해제 (Bitmap → BitmapSource 변환 후 필요)
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    // DPI 인식
    [DllImport("shcore.dll")]
    public static extern int SetProcessDpiAwareness(int value);

    // 수식키 modifier 상수
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // WM_HOTKEY 메시지
    public const int WM_HOTKEY = 0x0312;

    // ShowWindow nCmdShow
    public const int SW_RESTORE = 9;
}
