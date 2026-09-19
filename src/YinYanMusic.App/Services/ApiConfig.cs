namespace YinYanMusic.App.Services;

public static class ApiConfig
{
    public static string BaseUrl { get; set; } =
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.26.218.85:5116"
            : "http://localhost:5116";

    public static string Absolute(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        return $"{BaseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
    }
}