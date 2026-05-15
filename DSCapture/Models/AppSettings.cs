using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DSCapture.Models;

public class AppSettings
{
    [JsonPropertyName("save_dir")]
    public string SaveDir { get; set; } = AppContext.BaseDirectory;

    [JsonPropertyName("save_format")]
    public string SaveFormat { get; set; } = "png";

    [JsonPropertyName("shortcuts")]
    public ShortcutSettings Shortcuts { get; set; } = new();

    [JsonPropertyName("close_action")]
    public string CloseAction { get; set; } = "tray";

    [JsonPropertyName("box_width")]
    public int BoxWidth { get; set; } = 800;

    [JsonPropertyName("box_height")]
    public int BoxHeight { get; set; } = 600;

    [JsonPropertyName("drag_ratio")]
    public string DragRatio { get; set; } = "4:3";

    [JsonPropertyName("recent_captures")]
    public List<string> RecentCaptures { get; set; } = new();
}

public class ShortcutSettings
{
    [JsonPropertyName("fixed")]
    public string? Fixed { get; set; }

    [JsonPropertyName("drag")]
    public string? Drag { get; set; }

    [JsonPropertyName("full")]
    public string? Full { get; set; }
}
