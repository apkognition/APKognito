using System.IO;
using APKognito.ApkLib.Configuration;
using APKognito.Base.MVVM;

namespace APKognito.Utilities;

public static class UtilityInstaller
{
    private const string Apktool = "Apktool",
                         Apksigner = "Apksigner";

    public static async Task<PackageToolingPaths> VerifyToolInstallationsAsync(IViewLogger logger, CancellationToken token)
    {
        Dictionary<string, UtilityInformation> utilities = GetUtilityMap();

        foreach (UtilityInformation? utility in utilities
            .Where(p => !File.Exists(p.Value.UtilityPath))
            .Select(p => p.Value))
        {
            logger.Log($"Installing {utility.UtilityName}");
            await InstallUtilityAsync(utility.UtilityPath, utility.DownloadUrl, utility.InstallType, logger, token);
        }

        return new()
        {
            ApkToolJarPath = utilities[Apktool].UtilityPath,
            ApkSignerJarPath = utilities[Apksigner].UtilityPath,
        };
    }

    private static async Task InstallUtilityAsync(string filePath, string utilityUrl, UtilityInstallType installType, IViewLogger logger, CancellationToken token)
    {
        try
        {
            switch (installType)
            {
                case UtilityInstallType.GithubRelease:
                    await DownloadGithubReleaseAsync(filePath, utilityUrl, logger, token);
                    break;

                case UtilityInstallType.RegularFile:
                    await DownloadRegularFileAsync(filePath, utilityUrl, logger, token);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error while dispatching installation: {ex.Message}");
        }
    }

    private static async Task DownloadGithubReleaseAsync(string filePath, string utilityUrl, IViewLogger logger, CancellationToken token)
    {
        await WebGet.FetchAndDownloadGitHubReleaseAsync(utilityUrl, filePath, logger, token, 1);
    }

    private static async Task DownloadRegularFileAsync(string filePath, string utilityUrl, IViewLogger logger, CancellationToken token)
    {
        await WebGet.DownloadAsync(utilityUrl, filePath, logger, token);
    }

    private static Dictionary<string, UtilityInformation> GetUtilityMap()
    {
        string path = App.AppDataDirectory.FullName;

        return new()
        {
            [Apktool] = new(
                "Apktool",
                Path.Combine(path, "apktool.jar"),
                Constants.APKTOOL_JAR_URL_LTST,
                UtilityInstallType.GithubRelease
            ),
            [Apksigner] = new(
                "Uber APK Signer",
                Path.Combine(path, "uber-apk-signer.jar"),
                Constants.APK_SIGNER_URL_LTST,
                UtilityInstallType.GithubRelease
            )
        };
    }

    private sealed record UtilityInformation(
        string UtilityName,
        string UtilityPath,
        string DownloadUrl,
        UtilityInstallType InstallType
    );

    private enum UtilityInstallType
    {
        GithubRelease,
        RegularFile
    }
}
