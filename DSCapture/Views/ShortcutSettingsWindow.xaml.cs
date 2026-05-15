using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DSCapture.Models;

namespace DSCapture.Views;

public partial class ShortcutSettingsWindow : Window
{
    public ShortcutSettings? Result { get; private set; }

    private static readonly string[] Keys =
        Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
        .Concat(Enumerable.Range(0, 10).Select(i => i.ToString()))
        .Concat(Enumerable.Range(1, 12).Select(i => $"F{i}"))
        .Prepend("없음")
        .ToArray();

    public ShortcutSettingsWindow(ShortcutSettings current)
    {
        InitializeComponent();

        foreach (var cb in new[] { FixedKey, DragKey, FullKey })
            cb.ItemsSource = Keys;

        ApplyToUi(current.Fixed,  FixedCtrl, FixedAlt, FixedShift, FixedKey);
        ApplyToUi(current.Drag,   DragCtrl,  DragAlt,  DragShift,  DragKey);
        ApplyToUi(current.Full,   FullCtrl,  FullAlt,  FullShift,  FullKey);
    }

    private static void ApplyToUi(string? hotkeyStr,
        CheckBox ctrl, CheckBox alt, CheckBox shift, ComboBox key)
    {
        if (string.IsNullOrEmpty(hotkeyStr)) { key.SelectedIndex = 0; return; }
        var parts = hotkeyStr.ToLower().Split('+');
        ctrl.IsChecked  = parts.Contains("ctrl");
        alt.IsChecked   = parts.Contains("alt");
        shift.IsChecked = parts.Contains("shift");
        string k = parts.Last().ToUpper();
        key.SelectedItem = Keys.Contains(k) ? k : "없음";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Result = new ShortcutSettings
        {
            Fixed = BuildHotkeyStr(FixedCtrl, FixedAlt, FixedShift, FixedKey),
            Drag  = BuildHotkeyStr(DragCtrl,  DragAlt,  DragShift,  DragKey),
            Full  = BuildHotkeyStr(FullCtrl,  FullAlt,  FullShift,  FullKey),
        };
        DialogResult = true;
    }

    private static string? BuildHotkeyStr(
        CheckBox ctrl, CheckBox alt, CheckBox shift, ComboBox key)
    {
        string? k = key.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(k) || k == "없음") return null;

        var parts = new System.Collections.Generic.List<string>();
        if (ctrl.IsChecked  == true) parts.Add("ctrl");
        if (alt.IsChecked   == true) parts.Add("alt");
        if (shift.IsChecked == true) parts.Add("shift");
        parts.Add(k.ToLower());
        return string.Join("+", parts);
    }
}
