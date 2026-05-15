using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace DSCapture.Services;

public class HwidService
{
    /// <summary>
    /// 마더보드 + 디스크 시리얼을 SHA-256 해싱하여 XXXX-XXXX-XXXX 형식의 HWID 반환.
    /// Python get_hwid() 와 동일한 알고리즘.
    /// </summary>
    public string GetHwid()
    {
        try
        {
            string mb = QueryWmi("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
            string disk = QueryWmi("SELECT SerialNumber FROM Win32_DiskDrive", "SerialNumber");

            string raw = $"DS_{mb}_{disk}";
            string hash = Sha256Hex(raw).ToUpper();
            return $"{hash[..4]}-{hash[4..8]}-{hash[8..12]}";
        }
        catch
        {
            return FallbackHwid();
        }
    }

    private static string QueryWmi(string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject obj in searcher.Get())
        {
            var val = obj[property]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val))
                return val;
        }
        return "UNKNOWN";
    }

    private static string FallbackHwid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            string? guid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrEmpty(guid))
            {
                string hash = Sha256Hex(guid).ToUpper();
                return $"G-{hash[..4]}-{hash[4..8]}";
            }
        }
        catch { }
        return "ERR-0000";
    }

    private static string Sha256Hex(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
