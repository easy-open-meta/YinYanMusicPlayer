using System.Text;
using System.Text.Json;
using YinYanMusic.Core.Dtos;
#if WINDOWS
using System.Security.Cryptography;
#endif

namespace YinYanMusic.App.Services;

public interface IAuthService
{
    bool IsLoggedIn { get; }
    UserDto? CurrentUser { get; }
    string? Token { get; }
    Task<bool> TryRestoreSessionAsync();
    Task<AuthResponse> LoginAsync(string userName, string password);
    Task<AuthResponse> RegisterAsync(string userName, string password, string displayName, string? gender, DateOnly? birthday);
    Task LogoutAsync();
}

public class AuthService(IMusicApi api) : IAuthService
{
    private const string TokenKey = "auth_token";
#if WINDOWS
    private static string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "auth_token.enc");
#endif


    public string? Token { get; private set; }
    public UserDto? CurrentUser { get; private set; }
    public bool IsLoggedIn => Token is not null;

    public async Task<bool> TryRestoreSessionAsync()
    {
        Token = await ReadTokenAsync();
        if (Token is null) return false;
        if (IsTokenExpired(Token))
        {
            await LogoutAsync();
            return false;
        }
        try
        {
            CurrentUser = await api.MeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] TryRestoreSessionAsync MeAsync failed: {ex.Message}");
            await LogoutAsync();
            return false;
        }
        if (CurrentUser is null)
        {
            await LogoutAsync();
            return false;
        }
        return true;
    }

    public async Task<AuthResponse> LoginAsync(string userName, string password)
    {
        var resp = await api.LoginAsync(new LoginRequest(userName, password));
        await SaveSessionAsync(resp);
        return resp;
    }

    public async Task<AuthResponse> RegisterAsync(string userName, string password, string displayName, string? gender, DateOnly? birthday)
    {
        var resp = await api.RegisterAsync(new RegisterRequest(userName, password, displayName, gender, birthday));
        await SaveSessionAsync(resp);
        return resp;
    }

    public async Task LogoutAsync()
    {
        Token = null;
        CurrentUser = null;
        await RemoveTokenAsync();
    }

    private async Task SaveSessionAsync(AuthResponse resp)
    {
        Token = resp.AccessToken;
        CurrentUser = resp.User;
        await WriteTokenAsync(resp.AccessToken);
    }

    private static async Task<string?> ReadTokenAsync()
    {
#if WINDOWS
        try
        {
            if (!File.Exists(TokenFilePath)) return null;
            var encrypted = File.ReadAllBytes(TokenFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch { return null; }
#else
        try
        {
            return await SecureStorage.GetAsync(TokenKey);
        }
        catch { return null; }
#endif
    }

    private static async Task WriteTokenAsync(string token)
    {
#if WINDOWS
        try
        {
            var dir = Path.GetDirectoryName(TokenFilePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var bytes = Encoding.UTF8.GetBytes(token);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(TokenFilePath, encrypted);
        }
        catch { }
#else
        try { await SecureStorage.SetAsync(TokenKey, token); } catch { }
#endif
    }

    private static async Task RemoveTokenAsync()
    {
#if WINDOWS
        try { if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath); } catch { }
#else
        try { SecureStorage.Remove(TokenKey); } catch { }
#endif
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return true;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return true;
            if (!doc.RootElement.TryGetProperty("exp", out var expEl)) return true;
            if (expEl.ValueKind != JsonValueKind.Number) return true;
            if (!expEl.TryGetInt64(out var expUnix)) return true;
            var exp = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            return exp <= DateTimeOffset.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}