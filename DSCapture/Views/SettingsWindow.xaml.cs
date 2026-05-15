using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DSCapture.Models;
using DSCapture.Services;
using Microsoft.Win32;

namespace DSCapture.Views;

public partial class SettingsWindow : Window
{
    private const string BuildVersion = "v1.00";
    private const string BuildDate = "2026-05-08";

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Action _onShortcutChanged;

    public SettingsWindow(AppSettings settings, SettingsService settingsService,
        Action onShortcutChanged)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _onShortcutChanged = onShortcutChanged;

        VersionLabel.Text = $"Version: {BuildVersion}";
        BuildLabel.Text = $"Build: {BuildDate}";

        RefreshButtons();
        CurrentSaveDirLabel.Text = _settings.SaveDir;
    }

    private void RefreshButtons()
    {
        string activeColor = "#00d2d3";
        string inactiveColor = "#4b6584";

        BtnPng.Background = new System.Windows.Media.SolidColorBrush(
            ParseColor(_settings.SaveFormat == "png" ? activeColor : inactiveColor));
        BtnJpg.Background = new System.Windows.Media.SolidColorBrush(
            ParseColor(_settings.SaveFormat == "jpg" ? activeColor : inactiveColor));

        BtnTray.Background = new System.Windows.Media.SolidColorBrush(
            ParseColor(_settings.CloseAction == "tray" ? activeColor : inactiveColor));
        BtnExit.Background = new System.Windows.Media.SolidColorBrush(
            ParseColor(_settings.CloseAction == "exit" ? activeColor : inactiveColor));

        bool isStartup = CheckStartup();
        BtnStartupOn.Background  = new System.Windows.Media.SolidColorBrush(
            ParseColor(isStartup ? activeColor : inactiveColor));
        BtnStartupOff.Background = new System.Windows.Media.SolidColorBrush(
            ParseColor(!isStartup ? activeColor : inactiveColor));
    }

    private static System.Windows.Media.Color ParseColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    private void OnShortcutClick(object s, RoutedEventArgs e)
    {
        var win = new ShortcutSettingsWindow(_settings.Shortcuts);
        win.Owner = this;
        if (win.ShowDialog() == true)
        {
            _settings.Shortcuts = win.Result!;
            _settingsService.Save(_settings);
            _onShortcutChanged();
        }
    }

    private void OnSaveDirClick(object s, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
            { SelectedPath = _settings.SaveDir };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _settings.SaveDir = dlg.SelectedPath;
            _settingsService.Save(_settings);
            CurrentSaveDirLabel.Text = _settings.SaveDir;
        }
    }

    private void OnOpenFolderClick(object s, RoutedEventArgs e)
    {
        if (Directory.Exists(_settings.SaveDir))
            Process.Start("explorer.exe", _settings.SaveDir);
    }

    private void OnPngClick(object s, RoutedEventArgs e) => SetFormat("png");
    private void OnJpgClick(object s, RoutedEventArgs e) => SetFormat("jpg");
    private void SetFormat(string fmt)
    {
        _settings.SaveFormat = fmt;
        _settingsService.Save(_settings);
        RefreshButtons();
    }

    private void OnTrayClick(object s, RoutedEventArgs e) => SetClose("tray");
    private void OnExitClick(object s, RoutedEventArgs e) => SetClose("exit");
    private void SetClose(string action)
    {
        _settings.CloseAction = action;
        _settingsService.Save(_settings);
        RefreshButtons();
    }

    private void OnStartupOnClick(object s, RoutedEventArgs e)  => SetStartup(true);
    private void OnStartupOffClick(object s, RoutedEventArgs e) => SetStartup(false);
    private void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (enable)
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            key?.SetValue("DSCapture", $"\"{exePath}\" --startup");
        }
        else
        {
            key?.DeleteValue("DSCapture", throwOnMissingValue: false);
        }
        RefreshButtons();
    }

    private static bool CheckStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("DSCapture") != null;
    }
}
