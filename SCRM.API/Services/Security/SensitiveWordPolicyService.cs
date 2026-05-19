using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;

namespace SCRM.API.Services.Security;

/// <summary>
/// 服务端敏感词策略服务。
/// <para>复用 system_configs，不新增表；支持全局、设备、账号三个层级，便于和 Android 本地违禁词保持一致。</para>
/// </summary>
public class SensitiveWordPolicyService
{
    public const string BlockConfigKey = "security.sensitive_words.block";
    public const string WarnConfigKey = "security.sensitive_words.warn";
    public const string AuditConfigKey = "security.sensitive_words.audit";

    public const string DeviceBlockConfigPrefix = "security.sensitive_words.block.device.";
    public const string DeviceWarnConfigPrefix = "security.sensitive_words.warn.device.";
    public const string DeviceAuditConfigPrefix = "security.sensitive_words.audit.device.";

    public const string AccountBlockConfigPrefix = "security.sensitive_words.block.account.";
    public const string AccountWarnConfigPrefix = "security.sensitive_words.warn.account.";
    public const string AccountAuditConfigPrefix = "security.sensitive_words.audit.account.";

    private static readonly char[] WordSeparators =
    {
        '\r', '\n', '\t', ',', '，', ';', '；', '|'
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<SensitiveWordPolicyService> _logger;

    public SensitiveWordPolicyService(ApplicationDbContext db, ILogger<SensitiveWordPolicyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 读取当前发送上下文的敏感词策略。
    /// <para>读取顺序为全局 + 设备 + 账号，最终去重合并；缺省配置为空时默认放行。</para>
    /// </summary>
    public async Task<SensitiveWordPolicy> GetPolicyAsync(
        string? deviceUuid = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var keyMap = BuildPolicyKeys(deviceUuid, accountId);
        var allKeys = keyMap.Values
            .SelectMany(keys => keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (allKeys.Count == 0)
        {
            return SensitiveWordPolicy.Empty;
        }

        var configs = await _db.SystemConfigs
            .AsNoTracking()
            .Where(config => allKeys.Contains(config.key))
            .ToListAsync(cancellationToken);

        var valuesByKey = configs.ToDictionary(config => config.key, config => config.value ?? string.Empty, StringComparer.Ordinal);
        return new SensitiveWordPolicy(
            BuildWords(valuesByKey, keyMap["block"]),
            BuildWords(valuesByKey, keyMap["warn"]),
            BuildWords(valuesByKey, keyMap["audit"]));
    }

    /// <summary>
    /// 保存某设备当前 Android 违禁词对应的服务端阻断词库。
    /// <para>不新增表；只写 device/account 层级键，避免一个设备清空违禁词时误清全局策略。</para>
    /// </summary>
    public async Task SaveDeviceBlockWordsAsync(
        string? deviceUuid,
        string? accountId,
        IEnumerable<string>? words,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceUuid = NormalizeId(deviceUuid);
        var normalizedAccountId = NormalizeId(accountId);
        if (string.IsNullOrWhiteSpace(normalizedDeviceUuid) && string.IsNullOrWhiteSpace(normalizedAccountId))
        {
            return;
        }

        var normalizedWords = NormalizeWords(words).ToList();
        var value = string.Join('\n', normalizedWords);
        var now = DateTime.UtcNow;

        var updates = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(normalizedDeviceUuid))
        {
            updates[BuildScopedConfigKey(DeviceBlockConfigPrefix, normalizedDeviceUuid)] = "设备级服务端发送前阻断词库，与下发 Android 违禁词同步";
        }

        if (!string.IsNullOrWhiteSpace(normalizedAccountId))
        {
            updates[BuildScopedConfigKey(AccountBlockConfigPrefix, normalizedAccountId)] = "账号级服务端发送前阻断词库，与下发 Android 违禁词同步";
        }

        foreach (var item in updates)
        {
            var config = await _db.SystemConfigs.FirstOrDefaultAsync(config => config.key == item.Key, cancellationToken);
            if (config == null)
            {
                config = new SystemConfig
                {
                    key = item.Key,
                    value = value,
                    description = item.Value,
                    updatedAt = now
                };
                await _db.SystemConfigs.AddAsync(config, cancellationToken);
            }
            else
            {
                config.value = value;
                config.description = item.Value;
                config.updatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "已同步服务端敏感词阻断策略。DeviceUuid={DeviceUuid}, AccountId={AccountId}, Count={Count}",
            normalizedDeviceUuid,
            string.IsNullOrWhiteSpace(normalizedAccountId) ? "-" : normalizedAccountId,
            normalizedWords.Count);
    }

    /// <summary>
    /// 解析配置文本或输入数组为去重词表。
    /// </summary>
    public static IReadOnlyList<string> NormalizeWords(IEnumerable<string>? words)
    {
        if (words == null)
        {
            return Array.Empty<string>();
        }

        return words
            .SelectMany(SplitWords)
            .Select(word => word.Trim())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 构建设备级配置键。
    /// </summary>
    public static string BuildDeviceBlockConfigKey(string deviceUuid)
    {
        return BuildScopedConfigKey(DeviceBlockConfigPrefix, deviceUuid);
    }

    /// <summary>
    /// 构建账号级配置键。
    /// </summary>
    public static string BuildAccountBlockConfigKey(string accountId)
    {
        return BuildScopedConfigKey(AccountBlockConfigPrefix, accountId);
    }

    private static Dictionary<string, List<string>> BuildPolicyKeys(string? deviceUuid, string? accountId)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["block"] = new List<string> { BlockConfigKey },
            ["warn"] = new List<string> { WarnConfigKey },
            ["audit"] = new List<string> { AuditConfigKey }
        };

        var normalizedDeviceUuid = NormalizeId(deviceUuid);
        if (!string.IsNullOrWhiteSpace(normalizedDeviceUuid))
        {
            result["block"].Add(BuildScopedConfigKey(DeviceBlockConfigPrefix, normalizedDeviceUuid));
            result["warn"].Add(BuildScopedConfigKey(DeviceWarnConfigPrefix, normalizedDeviceUuid));
            result["audit"].Add(BuildScopedConfigKey(DeviceAuditConfigPrefix, normalizedDeviceUuid));
        }

        var normalizedAccountId = NormalizeId(accountId);
        if (!string.IsNullOrWhiteSpace(normalizedAccountId))
        {
            result["block"].Add(BuildScopedConfigKey(AccountBlockConfigPrefix, normalizedAccountId));
            result["warn"].Add(BuildScopedConfigKey(AccountWarnConfigPrefix, normalizedAccountId));
            result["audit"].Add(BuildScopedConfigKey(AccountAuditConfigPrefix, normalizedAccountId));
        }

        return result;
    }

    private static IReadOnlyList<string> BuildWords(IReadOnlyDictionary<string, string> valuesByKey, IEnumerable<string> keys)
    {
        return NormalizeWords(keys
            .Where(valuesByKey.ContainsKey)
            .Select(key => valuesByKey[key]));
    }

    private static IEnumerable<string> SplitWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeId(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string BuildScopedConfigKey(string prefix, string value)
    {
        var normalized = NormalizeId(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return prefix.TrimEnd('.');
        }

        var key = prefix + normalized;
        if (key.Length <= 100)
        {
            return key;
        }

        // system_configs.key 限长 100；极端长账号/设备标识使用稳定哈希后缀。
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)))[..32];
        return prefix + hash;
    }
}

/// <summary>
/// 服务端敏感词策略快照。
/// </summary>
public sealed record SensitiveWordPolicy(
    IReadOnlyList<string> BlockWords,
    IReadOnlyList<string> WarnWords,
    IReadOnlyList<string> AuditWords)
{
    public static SensitiveWordPolicy Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public bool IsEmpty => BlockWords.Count == 0 && WarnWords.Count == 0 && AuditWords.Count == 0;
}
