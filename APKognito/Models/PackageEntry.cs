using APKognito.Helpers;
using Newtonsoft.Json;

namespace APKognito.Models;

public record MinimalPackageInfo(
    [JsonProperty("package_path")]
    string PackagePath,

    string? AssetsPath
);

public sealed record PackageEntry : MinimalPackageInfo
{
    [JsonProperty("package_name")]
    public string PackageName { get; }

    public string? AssetPath { get; }

    [JsonProperty("package_size")]
    public long PackageSizeBytes { get; }

    [JsonProperty("assets_size")]
    public long AssetsSizeBytes { get; }

    [JsonProperty("data_size")]
    public long SaveDataSizeBytes { get; }

    public PackageEntry(string packageName, string packagePath, long packageSizeBytes, string? assetPath, long assetsSizeBytes, long saveDataSizeBytes)
        : base(packagePath, NullifyIfEmpty(assetPath))
    {
        PackageName = packageName;
        PackageSizeBytes = packageSizeBytes * 1024;
        AssetsSizeBytes = assetsSizeBytes * 1024;
        SaveDataSizeBytes = saveDataSizeBytes * 1024;
    }

    public string FormattedAssetsSize => AssetsSizeBytes < 0
        ? "(no assets)"
        : GBConverter.FormatSizeFromBytes(AssetsSizeBytes);

    public string FormattedPackageSize => GBConverter.FormatSizeFromBytes(PackageSizeBytes);

    public string FormattedSaveDataSize => SaveDataSizeBytes < 0
        ? "(no save data)"
        : GBConverter.FormatSizeFromBytes(SaveDataSizeBytes);

    public string FormattedTotalSize => GBConverter.FormatSizeFromBytes(PackageSizeBytes
        + Math.Max(AssetsSizeBytes, 0)
        + Math.Max(SaveDataSizeBytes, 0));

    private static string? NullifyIfEmpty(string? value)
    {
        // Ensures an empty path path is stored as null
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
