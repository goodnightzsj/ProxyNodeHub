using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProxyNodeHub;

// ── JSON 源码生成上下文 (裁剪安全 + 序列化零反射/更快) ──
[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(CacheData), TypeInfoPropertyName = "CacheData")]
[JsonSerializable(typeof(List<RepoInfo>), TypeInfoPropertyName = "RepoList")]
[JsonSerializable(typeof(SubscriptionLink), TypeInfoPropertyName = "SubscriptionLink")]
[JsonSerializable(typeof(GitHubCommit), TypeInfoPropertyName = "GitHubCommit")]
[JsonSerializable(typeof(CommitDetail), TypeInfoPropertyName = "CommitDetail")]
[JsonSerializable(typeof(GitHubUser), TypeInfoPropertyName = "GitHubUser")]
[JsonSerializable(typeof(BatchRepoResult), TypeInfoPropertyName = "BatchRepoResult")]
[JsonSerializable(typeof(GitHubRepo), TypeInfoPropertyName = "GitHubRepo")]
[JsonSerializable(typeof(AppSettings), TypeInfoPropertyName = "AppSettings")]
[JsonSerializable(typeof(GitHubVariables), TypeInfoPropertyName = "GitHubVariables")]
[JsonSerializable(typeof(KnownRepoConfig), TypeInfoPropertyName = "KnownRepoConfig")]
internal partial class AppJsonContext : JsonSerializerContext;

// ── 模型 ──
public class GitHubRepo
{
    [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("stargazers_count")] public int Stars { get; set; }
    [JsonPropertyName("forks_count")] public int Forks { get; set; }
    [JsonPropertyName("fork")] public bool Fork { get; set; }
    [JsonPropertyName("pushed_at")] public string? PushedAt { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("default_branch")] public string DefaultBranch { get; set; } = "main";
}

public class GitHubCommit
{
    [JsonPropertyName("commit")] public CommitDetail Commit { get; set; } = new();
    [JsonPropertyName("author")] public GitHubUser? Author { get; set; }
}

public class CommitDetail
{
    [JsonPropertyName("author")] public GitAuthor Author { get; set; } = new();
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

public class GitAuthor
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("date")] public string Date { get; set; } = "";
}

public class GitHubUser
{
    [JsonPropertyName("login")] public string? Login { get; set; }
}

public class RepoInfo
{
    public string FullName = "";
    public string? Description;
    public int Stars;
    public string? LastPush;
    public string? CreatedAt;
    public int DaysInactive;
    public int AgeDays;
    public int CommitsLast7Days;
    public int DistinctActiveDays;
    public bool IsActive;
    public string ProcessingType = "待分析";
    public int Score;
    public int TotalNodes;
    public bool CommitsAnalyzed;
    public List<SubscriptionLink> Links = new();

    public string StatusText => DaysInactive <= 0 ? "活跃中" : $"{DaysInactive}天未更新";
    public string Url => $"https://github.com/{FullName}";
}

public class SubscriptionLink
{
    public string Name = "";
    public string Url = "";
    public string Type = "";
    public int NodeCount;   // -1 = 待分析, 0 = 无效/无节点, >0 = 已验证节点数
    public bool IsValid = true;
    public bool IsAnalyzed;  // 是否已完成分析
}

public class CacheData
{
    public DateTime SavedAt { get; set; }
    public List<RepoInfo> Repos { get; set; } = new();
}

public class BatchRepoResult
{
    public string FullName = "";
    public string? PushedAt;
    public string? CreatedAt;
    public int Stars;
    public int CommitCount;
    public List<GitHubCommit> Commits = new();
}

/// GraphQL 请求体 (避免匿名类型, 裁剪/Native AOT 安全)
public class GitHubVariables
{
    public Dictionary<string, string> data = new();
}
