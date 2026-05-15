using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSCapture.Models;

namespace DSCapture.Services;

public class SettingsService
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings == null) return new AppSettings();

            // 존재하지 않는 파일은 목록에서 제거
            settings.RecentCaptures = settings.RecentCaptures
                .Where(File.Exists)
                .Take(10)
                .ToList();

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            // 최근 캡처 목록은 최대 10개
            settings.RecentCaptures = settings.RecentCaptures.Take(10).ToList();
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
