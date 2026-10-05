using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ProxyNodeHub;

public class AppSettings
{
    /// <summary>
    /// GitHub 个人访问令牌。明文存 exe 同目录 settings.json。
    ///
    /// 为什么不用 DPAPI 加密：DPAPI 绑定"当前用户 + 当前机器"，把 json 拷到
    /// 别的机器或换用户就解不出来（GetToken 返回空）。用户要的是"作为配置
    /// 放在 exe 同目录"——那意味着可随工具迁移、可手动查看编辑，明文才成立。
    /// 敏感字段放同目录本身是取舍：软件不联网时风险有限，明文换来的是可迁移。
    /// 首次打开默认 "",即未设置。
    /// </summary>
    public string GithubKey { get; set; } = "";
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

    /// <summary>内核可执行文件路径。空 = 用默认位置。</summary>
    public string KernelPath { get; set; } = "";

    /// <summary>自动检测内核新版本并静默安装，免去手动下载。</summary>
    public bool KernelAutoUpdate { get; set; } = true;

    /// <summary>用户已知悉内核为 GPL-3.0 且体积约 55MB（首次提示过就不再打扰）。</summary>
    public bool KernelNoticeShown { get; set; }

    private static string Dir
    {
        get
        {
            // 用户要求 key 作为配置放 exe 同目录的 json 文件，所以设置文件
            // 跟 exe 走 —— 工具拷到哪，设置就到哪（便携）。回退到 exe 当前
            // 目录，避免路径为空的极端情况。
            try
            {
                var dir = Path.GetDirectoryName(Application.ExecutablePath);
                return string.IsNullOrEmpty(dir) ? AppContext.BaseDirectory : dir;
            }
            catch { return AppContext.BaseDirectory; }
        }
    }

    private static string SettingsPath => Path.Combine(Dir, "settings.json");

    /// <summary>加载后的单例缓存。KernelPath 等 getter 每次都 Load，之前是每次
    /// 读盘反序列化；缓存后 Load 只读一次盘，之后全部走内存。</summary>
    private static AppSettings? _cache;

    public static AppSettings Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(SettingsPath))
                _cache = JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), AppJsonContext.Default.AppSettings) ?? new();
        }
        catch { }
        _cache ??= new AppSettings();
        return _cache;
    }

    /// <summary>显式失效缓存，强制下次 Load 重新读盘（理论上只有外部改文件才需要）。</summary>
    public static void Invalidate() => _cache = null;

    public void Save()
    {
        _cache = this;   // 同步缓存，避免 Save 后又被旧缓存覆盖
        try
        {
            AtomicFile.Write(SettingsPath, JsonSerializer.Serialize(this, AppJsonContext.Default.AppSettings));
        }
        catch { }
    }

    public string GetToken() => GithubKey ?? "";

    public void SetToken(string token) => GithubKey = token ?? "";
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
            var data = new CacheData { SavedAt = DateTime.Now, Repos = repos };
            AtomicFile.Write(CachePath, JsonSerializer.Serialize(data, AppJsonContext.Default.CacheData));
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

    /// <summary>收藏列表缓存。SpeedTestPanel 里三处都 Load，之前每次读盘。</summary>
    private static List<RepoInfo>? _cache;

    public static List<RepoInfo> Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(StorePath))
                _cache = JsonSerializer.Deserialize(File.ReadAllText(StorePath), AppJsonContext.Default.RepoList) ?? new();
        }
        catch { }
        _cache ??= new List<RepoInfo>();
        return _cache;
    }

    public static void Save(List<RepoInfo> repos)
    {
        _cache = repos;   // 同步缓存
        try
        {
            AtomicFile.Write(StorePath, JsonSerializer.Serialize(repos, AppJsonContext.Default.RepoList));
        }
        catch { }
    }
}
