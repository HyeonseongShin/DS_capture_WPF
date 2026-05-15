using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DSCapture.Models;

namespace DSCapture.Services;

public class LicenseService
{
    private const string SecretKey = "DS_CAPTURE_SECRET_KEY_2026_@!";

    private readonly HwidService _hwidService;

    public LicenseService(HwidService hwidService)
    {
        _hwidService = hwidService;
    }

    /// <summary>
    /// .lic 파일을 탐색하여 HWID + HMAC-SHA256 서명 검증 후 라이센스 유효 여부 반환.
    /// Python check_license() 와 동일한 검증 로직.
    /// </summary>
    public (bool IsValid, LicenseData? Data) CheckLicense(string appName)
    {
        string hwid = _hwidService.GetHwid();
        string exeDir = AppContext.BaseDirectory;

        string[] searchFolders = [@"C:\license", exeDir];
        string failReason = "";

        foreach (string folder in searchFolders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (string file in Directory.EnumerateFiles(folder, "*.lic"))
            {
                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var data = JsonSerializer.Deserialize<LicenseData>(json);
                    if (data == null) continue;

                    // 1. HWID 일치
                    if (data.Hwid != hwid) continue;

                    // 2. 앱 이름 일치
                    if (data.AppName != appName)
                    {
                        failReason = $"해당 라이센스는 {data.AppName}용입니다.";
                        continue;
                    }

                    // 3. HMAC-SHA256 서명 검증
                    string msg = $"{data.Hwid}{data.AppName}{data.ExpiryDate}{data.UserName}";
                    string expected = ComputeHmac(msg);
                    if (!string.Equals(data.Signature, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        failReason = "라이센스 서명이 올바르지 않습니다. (변조 또는 구버전 규격)";
                        continue;
                    }

                    // 4. 만료일 확인
                    if (data.ExpiryDate != "PERMANENT")
                    {
                        if (!DateTime.TryParseExact(data.ExpiryDate, "yyyy-MM-dd",
                            null, System.Globalization.DateTimeStyles.None, out var expiry))
                        {
                            failReason = "만료일 형식이 잘못되었습니다.";
                            continue;
                        }
                        if (DateTime.Now > expiry)
                        {
                            failReason = $"라이센스가 만료되었습니다. (만료일: {data.ExpiryDate})";
                            continue;
                        }
                    }

                    return (true, data);
                }
                catch { }
            }
        }

        string message = string.IsNullOrEmpty(failReason)
            ? $"유효한 라이센스 파일을 찾을 수 없습니다.\n(C:\\license 폴더에 .lic 파일을 넣어주세요)"
            : $"라이센스 검증 실패:\n{failReason}";

        ShowLicenseError(hwid, message);
        return (false, null);
    }

    private static string ComputeHmac(string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLower();
    }

    private static void ShowLicenseError(string hwid, string message)
    {
        var win = new Views.LicenseErrorWindow(hwid, message);
        win.ShowDialog();
    }
}
