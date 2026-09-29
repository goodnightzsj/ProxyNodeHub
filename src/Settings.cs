using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ProxyNodeHub;

public class AppSettings
{
    public string EncryptedToken { get; set; } = "";
    public int RepoCount { get; set; } = 30;
    public int MinCommitsPerDay { get; set; } = 0;
    public int MinActiveDays { get; set; } = 0;
    public int InactiveDays { get; set; } = 7;
    public bool AutoClean { get; set; } = true;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowW { get; set; } = 1180;
    public int WindowH { get; set; } = 740;
    public bool Maximized { get; set; }
    public bool InspectorOpen { get; set; }

    private static string Dir
    {
        get
        {
            try { return Application.UserAppDataPath; }
            catch { return Path.Combine(Path.GetTempPath(), "ProxyNodeHub"); }
        }
    }

    private static string SettingsPath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), AppJsonContext.Default.AppSettings) ?? new();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, AppJsonContext.Default.AppSettings));
        }
        catch { }
    }

    public string GetToken()
    {
        if (string.IsNullOrEmpty(EncryptedToken)) return "";
        try
        {
            var bytes = Convert.FromBase64String(EncryptedToken);
            var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch { return ""; }
    }

    public void SetToken(string token)
    {
        if (string.IsNullOrEmpty(token)) { EncryptedToken = ""; return; }
        try
        {
            var bytes = System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(token), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            EncryptedToken = Convert.ToBase64String(bytes);
        }
        catch { EncryptedToken = ""; }
    }
}

public static class ResultCache
{
    private static string Dir
    {
        get
        {
            try { return Application.UserAppDataPath; }
            catch { return Path.Combine(Path.GetTempPath(), "ProxyNodeHub"); }
        }
    }

    private static string CachePath => Path.Combine(Dir, "cache.json");

    public static void Save(List<RepoInfo> repos)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var data = new CacheData { SavedAt = DateTime.Now, Repos = repos };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(data, AppJsonContext.Default.CacheData));
        }
        catch { }
    }

    public static CacheData? Load()
    {
        try
        {
            if (File.Exists(CachePath))
                return JsonSerializer.Deserialize(File.ReadAllText(CachePath), AppJsonContext.Default.CacheData);
        }
        catch { }
        return null;
    }

    public static void Clear()
    {
        try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
    }
}

public static class FavoritesStore
{
    private static string Dir
    {
        get
        {
            try { return Application.UserAppDataPath; }
            catch { return Path.Combine(Path.GetTempPath(), "ProxyNodeHub"); }
        }
    }

    private static string StorePath => Path.Combine(Dir, "favorites.json");

    public static List<RepoInfo> Load()
    {
        try
        {
            if (File.Exists(StorePath))
                return JsonSerializer.Deserialize(File.ReadAllText(StorePath), AppJsonContext.Default.RepoList) ?? new();
        }
        catch { }
        return new List<RepoInfo>();
    }

    public static void Save(List<RepoInfo> repos)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(repos, AppJsonContext.Default.RepoList));
        }
        catch { }
    }
}
