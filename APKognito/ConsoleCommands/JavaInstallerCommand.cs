using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Services;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using APKognito.Base.MVVM;

namespace APKognito.ConsoleCommands;

internal static class JavaInstallerCommand
{
    [Command("install-jdk", "Install Java")]
    public static async Task InstallJdkCommandAsync(IViewLogger logger, CancellationToken token)
    {
        IWindowService windowService = App.GetService<IWindowService>()!;

        JavaDownloadInfo? javaDownloadInfo = windowService.PromptJavaInstallationWindow(logger);

        if (javaDownloadInfo is null)
        {
            logger.LogError("Aborting Java installation.");
            return;
        }

        string installerFilename = Path.GetFileName(javaDownloadInfo.DownloadUrl!);
        string tempDirectory = Path.Combine(Path.GetTempPath(), "APKognito-JdkInstaller");
        string installerPath = Path.Combine(tempDirectory, installerFilename);

        _ = DirectoryManager.CreateClaimedDirectory(tempDirectory);

        if (File.Exists(installerPath))
        {
            logger.Log("Using previously downloaded Java installer...");
        }
        else
        {
            logger.Log($"Downloading {installerFilename}, {GBConverter.FormatSizeFromBytes(javaDownloadInfo.DownloadSize)}");

            using IDisposable? scope = logger.BeginScope("WebGet");

            bool result = await WebGet.DownloadAsync(javaDownloadInfo.DownloadUrl!, installerPath, logger, token);

            if (!result)
            {
                logger.LogError($"Failed to download JDK {javaDownloadInfo.JavaVersion.Major}.");
                return;
            }
        }

        try
        {
            await RunJavaInstallerAsync(javaDownloadInfo, installerPath, logger, token);
        }
        catch (Win32Exception)
        {
            logger.LogWarning("Installer canceled.");
            return;
        }
        finally
        {
            if (File.Exists(installerPath))
            {
                logger.Log($"The JDK installer has not been deleted in case you want to install later. You can find it in the Drive Footprint page, or:\n{installerPath}");
            }
        }
    }

    private static async Task RunJavaInstallerAsync(JavaDownloadInfo downloadInfo, string installerPath, IViewLogger logger, CancellationToken token)
    {
        string installerArgs = $"/i \"{installerPath}\" /passive /norestart ALLUSERS=1 ARPSYSTEMCOMPONENT=0 INSTALLLEVEL=2";

        if (downloadInfo.InstallDirectory is not null)
        {
            installerArgs = $"{installerArgs} INSTALLDIR=\"{downloadInfo.InstallDirectory}\"";
            logger.Log($"Using custom installation directory: {downloadInfo.InstallDirectory}");
        }

#if DEBUG || PUBLIC_DEBUG
        string logfile = $"AdoptOpenJDK-MSI-{downloadInfo.JavaVersion}.log";
        installerArgs = $"{installerArgs} /l*v \"{Path.GetDirectoryName(installerPath)}/{logfile}\"";
        logger.LogDebug($"Using debug log file {logfile}");
#endif

        logger.LogDebug($"Running command:\nmsiexec.exe {installerArgs}");

        using Process installerProcess = new()
        {
            StartInfo = new()
            {
                FileName = "msiexec.exe",
                UseShellExecute = true,
                Arguments = installerArgs,
                Verb = "runas",
            }
        };

        logger.Log("Waiting for installer to exit...");
        _ = installerProcess.Start();
        await installerProcess.WaitForExitAsync(token);

        // 0: Install successful
        // 1602: Any kind of user decline during the installer (including if it was already installed and user was prompted for removal), or, if the installer feels like fucking with you.
        if (installerProcess.ExitCode is not 0)
        {
            logger.LogWarning($"Java install likely aborted (exit code {installerProcess.ExitCode})! Checking Java executable path...");

            string stderr = await installerProcess.StandardError.ReadToEndAsync(token);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogError(stderr);
            }
        }
        else
        {
            logger.LogSuccess($"JDK {downloadInfo.JavaVersion.Major} installed successfully! Checking Java executable path...");
        }

        JavaVersionInformation[] foundVersions = [.. JavaVersionCollector.RefreshJavaVersions().Where(v => v.Version == downloadInfo.JavaVersion)];

        if (foundVersions.Length is not 0)
        {
            logger.LogSuccess($"Detected {downloadInfo.JavaVersion}:\n{string.Join('\n', foundVersions)}");
        }
        else
        {
            logger.LogError("Failed to detect new JDK installation. Try restarting APKognito, your computer, or reinstalling.");
            return;
        }

        File.Delete(installerPath);
    }
}
