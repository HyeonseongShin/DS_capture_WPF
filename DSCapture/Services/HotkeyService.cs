using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using DSCapture.Helpers;
using DSCapture.Models;

namespace DSCapture.Services;

/// <summary>
/// Win32 RegisterHotKey를 이용한 전역 단축키 서비스.
/// Python keyboard.add_hotkey() 대체.
/// </summary>
public class HotkeyService : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 9000;

    public void Initialize(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>단축키 문자열("ctrl+shift+f")을 파싱하여 등록</summary>
    public bool Register(string hotkeyStr, Action callback)
    {
        if (string.IsNullOrWhiteSpace(hotkeyStr)) return false;

        var (modifiers, vk) = Parse(hotkeyStr);
        if (vk == 0) return false;

        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_hwnd, id,
            modifiers | NativeMethods.MOD_NOREPEAT, vk))
            return false;

        _handlers[id] = callback;
        return true;
    }

    /// <summary>등록된 단축키 모두 해제 후 ShortcutSettings 기반으로 재등록</summary>
    public void ApplyShortcuts(ShortcutSettings shortcuts,
        Action openFixed, Action openDrag, Action openFull)
    {
        UnregisterAll();
        if (shortcuts.Fixed != null) Register(shortcuts.Fixed, openFixed);
        if (shortcuts.Drag   != null) Register(shortcuts.Drag,  openDrag);
        if (shortcuts.Full   != null) Register(shortcuts.Full,  openFull);
    }

    public void UnregisterAll()
    {
        foreach (int id in _handlers.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
        _nextId = 9000;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static (uint modifiers, uint vk) Parse(string hotkeyStr)
    {
        uint mods = 0;
        uint vk = 0;
        foreach (string part in hotkeyStr.ToLower().Split('+'))
        {
            switch (part.Trim())
            {
                case "ctrl":  mods |= NativeMethods.MOD_CONTROL; break;
                case "alt":   mods |= NativeMethods.MOD_ALT;     break;
                case "shift": mods |= NativeMethods.MOD_SHIFT;   break;
                default:
                    if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                        vk = (uint)char.ToUpper(part[0]);
                    else if (part.StartsWith("f") && int.TryParse(part[1..], out int fn))
                        vk = (uint)(111 + fn); // F1=112, F2=113 ...
                    break;
            }
        }
        return (mods, vk);
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
