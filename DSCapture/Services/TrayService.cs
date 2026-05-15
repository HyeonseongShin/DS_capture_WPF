using System;
using System.Drawing;
using System.Windows.Forms;

namespace DSCapture.Services;

/// <summary>
/// System.Windows.Forms.NotifyIcon 기반 시스템 트레이 아이콘 서비스.
/// Python pystray 대체.
/// </summary>
public class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(Icon icon, Action onOpen, Action onOpenFolder, Action onExit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("열기 (Open)",       null, (_, _) => onOpen());
        menu.Items.Add("폴더 열기",          null, (_, _) => onOpenFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료 (Exit)",        null, (_, _) => onExit());

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "DS Capture",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => onOpen();
    }

    public void Show() => _notifyIcon.Visible = true;
    public void Hide() => _notifyIcon.Visible = false;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
