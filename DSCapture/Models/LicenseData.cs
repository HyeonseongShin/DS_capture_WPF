using System.Text.Json.Serialization;

namespace DSCapture.Models;

public class LicenseData
{
    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = "";

    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = "";

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = "";

    [JsonPropertyName("expiry_date")]
    public string ExpiryDate { get; set; } = "";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}
