using APKognito.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("misc.cache", ConfigType.Bson, ConfigModifiers.JsonIgnoreMissing)]
internal sealed class CacheStorage : IKognitoConfig
{
    /// <summary>
    /// Holds old APK paths to load (at least so there's content to present).
    /// </summary>
    [JsonProperty("asp")]
    public string? ApkSourcePath { get; set; }

    /// <summary>
    /// Specifies where to open the FileDialog to select an APK.
    /// </summary>
    [JsonProperty("ldd")]
    public string? LastDialogDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// The newest update location (Stored here as a poor attempt of preventing user tampering)
    /// </summary>
    [JsonProperty("usl")]
    public string? UpdateSourceLocation { get; set; }

    [JsonProperty("ifl")]
    public bool IsFirstLaunch { get; set; } = true;

    [JsonProperty("mll")]
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.INFO;

    [JsonProperty("letv")]
    public bool LogExceptionsToView { get; set; } = false;

    [JsonProperty("buckets")]
    public Dictionary<string, JToken> Buckets { get; private set; } = [];

    public void SetBucket<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        Buckets[key] = JToken.FromObject(value);
    }

    public T? GetBucket<T>(string key) where T : new()
    {
        return Buckets.TryGetValue(key, out JToken? token)
            ? token.ToObject<T>()
            : default;
    }
}
