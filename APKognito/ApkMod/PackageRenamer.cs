using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using APKognito.AdbTools;
using APKognito.ApkLib;
using APKognito.ApkLib.Automation;
using APKognito.ApkLib.Automation.Parser;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkLib.Exceptions;
using APKognito.Base.MVVM;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Exceptions;
using APKognito.Helpers;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkMod;

public record RenameBatchRequest(
    string[] SourcePackagePaths,
    UserRenameConfiguration UserRenameConfiguration,
    AdvancedApkRenameSettings AdvancedRenameConfiguration,
    string OutputDirectory
);

public sealed partial class PackageRenamer
{
    private readonly IViewLogger _logger;
    private readonly IProgress<ProgressInfo> _reporter;
    private readonly ConfigurationFactory _configurationFactory;

    public PackageRenamer(
        ConfigurationFactory configFactory,
        IViewLogger logger,
        IProgress<ProgressInfo> reporter)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(reporter);

        _configurationFactory = configFactory;
        _logger = logger;
        _reporter = reporter;
    }

    public async Task<PackageRenameResult> RenameBatchAsync(RenameBatchRequest batch, CancellationToken token)
    {


        PackageRenameConfiguration renameConfig = MapRenameSettings(packageContext);

        PackageRenameState nameState = new()
        {
            PackageAssemblyDirectory = packageContext.TempDirectory,
            PackageOutputDirectory = packageContext.OutputBaseDirectory,
            SmaliAssemblyDirectory = Path.Combine(Path.GetDirectoryName(packageContext.TempDirectory)!, "$smali"),
            SourcePackagePath = packageContext.SourcePackagePath,
            NewCompanyName = packageContext.UserRenameConfig.ApkNameReplacement,
        };
    }

    private async Task<PackageRenameResult> RenamePackageAsync(
        PackageContext packageContext,
        bool pushAfterRename,
        CancellationToken token = default)
    {
        try
        {
            PackageRenameResult result = await ProcessPackageAsync(packageContext, renameConfig, nameState, token);

            if (!result.Successful)
            {
                return result;
            }

            if (pushAfterRename)
            {
                await PushRenamedApkAsync(result.OutputLocations, token);
            }

            string claimFile = DirectoryManager.ClaimDirectory(Path.GetDirectoryName(result.OutputLocations.OutputApkPath)!);
            MetadataManager.WriteMetadata(claimFile, result.RenamedPackageMetadata);

            return result;
        }
        catch (TaskCanceledException tcex)
        {
            _logger.LogWarning(tcex, "Job canceled.");
            return new()
            {
                ResultStatus = "Job canceled.",
                Successful = false,
                OutputLocations = RenameOutputLocations.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed.");
            return new()
            {
                ResultStatus = ex.Message,
                Successful = false,
                OutputLocations = RenameOutputLocations.Empty
            };
        }
    }

    public async Task SideloadPackageAsync(RenameOutputLocations locations, CancellationToken token = default)
    {
        await PushRenamedApkAsync(locations, token);
    }

    public async Task SideloadPackageAsync(string fullPackagePath, RenamedPackageMetadata metadata, CancellationToken token = default)
    {
        var locations = new RenameOutputLocations(
            fullPackagePath,
            metadata.RelativeAssetsPath is not null
                ? Path.Combine(Path.GetDirectoryName(fullPackagePath)!, metadata.RelativeAssetsPath)
                : null,
            metadata.PackageName,
            string.Empty // Not used for sideloading
        );

        await PushRenamedApkAsync(locations, token);
    }

    private async Task<PackageRenameResult> ProcessPackageAsync(
        PackageContext modRenameConfig,
        PackageRenameConfiguration renameConfig,
        PackageRenameState nameState,
        CancellationToken token)
    {
        _reporter.Report(new(string.Empty, ProgressUpdateType.Reset));

        AutoConfig? automationConfig = modRenameConfig.AdvancedConfig.AutoPackageEnabled
            ? GetParsedAutoConfigAsync(modRenameConfig.AdvancedConfig.AutoPackageConfig)
            : null;

        PackageEditorContext context = new(renameConfig, modRenameConfig.ToolingPaths, nameState, _logger, _reporter);

        /* Unpack */

        PackageCompressor compressor = context.CreatePackageCompressor();
        await TimeAsync(async () =>
        {
            await compressor.UnpackPackageAsync(token: token);
            context.GatherPackageMetadata();
        }, nameof(compressor.UnpackPackageAsync));

        _ = await GetCommandResultAsync(automationConfig, CommandStage.Unpack, nameState);

        _logger.LogInformation("Changing '{OriginalName}' |> '{NewName}'", nameState.OldPackageName, nameState.NewPackageName);

        /* Rename stuff */

        await (renameConfig.UseBootstrapClassLoader
            ? InjectBootstrapperAsync(nameState, renameConfig.BootstrapConfiguration!)
            : RunBruteRenameAsync(nameState, automationConfig, context, token));

        /* Pack and Sign */

        await TimeAsync(async () => await compressor.PackPackageAsync(token: token), nameof(compressor.PackPackageAsync));
        await TimeAsync(async () => await compressor.SignPackageAsync(token: token), nameof(compressor.SignPackageAsync));

        /* Assets */

        string? outputAssetDirectory = null;
        await TimeAsync(async () =>
        {
            AssetEditor assetEditor = context.CreateAssetEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Assets, nameState));
            outputAssetDirectory = await assetEditor.RunAsync(token: token);
        }, nameof(AssetEditor));

        _ = await GetCommandResultAsync(automationConfig, CommandStage.Pack, nameState);

        /* Cleanup */

        await CleanUpTempsAsync(renameConfig, nameState, token);

        /* Finalize and return paths */

        string outputPackagePath = Path.Combine(nameState.PackageOutputDirectory, $"{nameState.NewPackageName}.apk");

        return new PackageRenameResult()
        {
            Successful = true,
            OutputLocations = new(outputPackagePath, outputAssetDirectory, nameState.NewPackageName, nameState.OldPackageName),
            RenamedPackageMetadata = new()
            {
                PackageName = nameState.NewPackageName,
                OriginalPackageName = nameState.OldPackageName,
                RelativeAssetsPath = outputAssetDirectory is not null
                    ? Path.GetRelativePath(Path.GetDirectoryName(outputPackagePath)!, outputAssetDirectory)
                    : null,
                RenameDate = DateTimeOffset.UtcNow,
                ApkognitoVersion = App.Version.GetVersion()
            },
        };
    }

    private async Task RunBruteRenameAsync(PackageRenameState nameState, AutoConfig? automationConfig, PackageEditorContext context, CancellationToken token)
    {
        /* Directories */

        await TimeAsync(async () =>
        {
            context.CreateDirectoryEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Directory, nameState))
                .Run();
        }, nameof(DirectoryEditor));

        /* Libraries */

        await TimeAsync(async () =>
        {
            LibraryEditor libraryEditor = context.CreateLibraryEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Library, nameState));
            await libraryEditor.RunAsync(token: token);
        }, nameof(LibraryEditor));

        /* Smali */

        await TimeAsync(async () =>
        {
            SmaliEditor smaliEditor = context.CreateSmaliEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Smali, nameState));
            await smaliEditor.RunAsync(token: token);
        }, nameof(SmaliEditor));
    }

    private async Task InjectBootstrapperAsync(PackageRenameState nameState, BootstrapConfiguration bootstrapConfig)
    {
        PackageBootstrapper bootstrapper = new(nameState.PackageAssemblyDirectory, bootstrapConfig, _logger);
        await TimeAsync(bootstrapper.RunAsync);
    }

    private static PackageRenameConfiguration MapRenameSettings(PackageContext settings)
    {
        string assetDirectory = Path.Combine(
            Path.GetDirectoryName(settings.SourcePackagePath)!,
            Path.GetFileNameWithoutExtension(settings.SourcePackagePath)
        );

        if (settings.AdvancedConfig.RenameType is RenameType.Bootstrapper)
        {
            if (string.IsNullOrWhiteSpace(settings.AdvancedConfig.NewBootstrapPackageName))
            {
                throw new InvalidConfigurationException("A bootstrap package name is required.");
            }

            if (!settings.AdvancedConfig.NewBootstrapPackageName.Contains('.'))
            {
                throw new InvalidConfigurationException("The bootstrap package name must be a valid package name (containing at least one identifier separating dot). e.g., io.sombody101.{appname}");
            }
        }

        return new()
        {
            // This will look weird if you're not using a font with ligatures.
            // Both my IDE and the main log output box use Fira Code, so it looks like an arrow.
            ReplacementInfoDelimiter = " |> ",
            ClearTempFilesOnRename = settings.UserRenameConfig.ClearTempFilesOnRename,
            RenameRegex = settings.AdvancedConfig.PackageReplaceRegexString,
            CompressorConfiguration = new()
            {
                ExtraJavaOptions = settings.AdvancedConfig.JavaFlags.Split().Where(s => !string.IsNullOrWhiteSpace(s))
            },
            DirectoryRenameConfiguration = new()
            {
            },
            LibraryRenameConfiguration = new()
            {
                EnableLibraryFileRenaming = settings.AdvancedConfig.RenameLibs,
                EnableLibraryRenaming = settings.AdvancedConfig.RenameLibsInternal,
                ExtraInternalPackagePaths = settings.AdvancedConfig.ExtraInternalPackagePaths,
            },
            SmaliRenameConfiguration = new()
            {
                ExtraInternalPackagePaths = settings.AdvancedConfig.ExtraInternalPackagePaths,
                SmaliBufferSize = settings.AdvancedConfig.SmaliBufferSize,
                MaxSmaliLoadSize = settings.AdvancedConfig.SmaliCutoffLimit,
            },
            AssetRenameConfiguration = new()
            {
                AssetDirectory = assetDirectory,
                CopyAssets = settings.UserRenameConfig.CopyFilesWhenRenaming,
                RenameObbArchiveEntries = settings.AdvancedConfig.RenameObbsInternal,
                ExtraInternalPackagePaths = [.. settings.AdvancedConfig.RenameObbsInternalExtras],
            },
            UseBootstrapClassLoader = settings.AdvancedConfig.RenameType is RenameType.Bootstrapper,
            BootstrapConfiguration = new()
            {
                NewPackageName = settings.AdvancedConfig.NewBootstrapPackageName,
                //FriendlyAppName = settings.AdvancedConfig.FriendlyBootstrapAppName,
                EnableErrorReporting = settings.AdvancedConfig.EnableBootstrapErrorReporting,
            },
        };
    }

    private AutoConfig? GetParsedAutoConfigAsync(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            _logger.LogInformation("Found auto config is null or empty (won't be parsed).");
            return null;
        }

        // We all know damn well this is not compiling, but "parsing" didn't sound as cool :p
        _logger.LogInformation("Compiling auto configuration...");

        return Tools.Time(() =>
        {
            return new AutoConfigParser(_logger).ParseDocument(config);
        });
    }

    private async Task<CommandStageResult?> GetCommandResultAsync(AutoConfig? config, CommandStage stage, PackageRenameState nameState)
    {
        if (config is null)
        {
            return null;
        }

        RenameStage? foundStage = config.GetStage(stage);

        if (foundStage is null)
        {
            _logger.LogDebug("No stage found for {Stage}, no alterations made.", stage);
            return null;
        }

        _logger.LogInformation("-- Entering auto configuration script for stage {Stage}.", stage);

        try
        {
            Dictionary<string, string> variables = new()
            {
                { "originalCompany", nameState.OldCompanyName ?? string.Empty },
                { "originalPackage", nameState.OldPackageName ?? string.Empty },
                { "newCompany", nameState.NewCompanyName },
                { "newPackage", nameState.NewPackageName },
            };

            using IDisposable? scope = _logger.BeginScope("[SCRIPT]");
            return await new CommandDispatcher(foundStage, nameState.PackageAssemblyDirectory, variables, _logger)
                .DispatchCommandsAsync();
        }
        finally
        {
            _logger.LogInformation("-- Exiting auto configuration script for stage {Stage}.", stage);
        }
    }

    private async Task PushRenamedApkAsync(RenameOutputLocations locations, CancellationToken cancellationToken)
    {
        if (locations.OutputApkPath is null)
        {
            _logger.LogError("Renamed APK path is null. Cannot push to device.");
            return;
        }

        AdbDeviceInfo? currentDevice = _configurationFactory.GetConfig<AdbConfig>().GetCurrentDevice();

        if (currentDevice is null)
        {
            const string error = "Failed to get ADB device profile. Make sure your device is connected and selected in the Android Device menu";
            _logger.LogError(error);
            throw new AdbPushFailedException(Path.GetFileName(locations.NewPackageName), error);
        }

        FileInfo apkInfo = new(locations.OutputApkPath);

        if (string.IsNullOrWhiteSpace(locations.NewPackageName))
        {
            _logger.LogError("Failed to get new package name from location output data. Aborting package upload.");
            return;
        }

        _logger.Log($"Installing {locations.NewPackageName} to {currentDevice.DeviceId} ({GBConverter.FormatSizeFromBytes(apkInfo.Length)})");

        await AdbManager.WakeDeviceAsync();
        _ = await AdbManager.InstallPackageAsync(apkInfo.FullName, token: cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(locations.AssetsDirectory))
        {
            if (!Directory.Exists(locations.AssetsDirectory))
            {
                _logger.LogError("Failed to find the assets directory at: {Path}", locations.AssetsDirectory);
                return;
            }

            await UploadPackageAssetsAsync(locations, currentDevice, cancellationToken);
        }

        _logger.Log($"Install complete.");
    }

    private async Task UploadPackageAssetsAsync(RenameOutputLocations locations, AdbDeviceInfo currentDevice, CancellationToken token)
    {
        if (locations.AssetsDirectory is null)
        {
            _logger.Log("No assets to upload.");
            return;
        }

        string[] assets = Directory.GetFiles(locations.AssetsDirectory);

        string obbDirectory = $"{AdbManager.ANDROID_OBB}/{locations.NewPackageName}";
        _logger.Log($"Pushing {assets.Length} asset(s) to {currentDevice.DeviceId}: {obbDirectory}");

        _ = await AdbManager.QuickDeviceCommandAsync(@$"shell [ -d ""{obbDirectory}"" ] && rm -r ""{obbDirectory}""; mkdir ""{obbDirectory}""", token: token);

        using IDisposable? scope = _logger.BeginScope("Upload");

        int assetIndex = 0;
        foreach (string file in assets)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            var assetInfo = new FileInfo(file);
            _logger.Log($"Pushing [{++assetIndex}/{assets.Length}]: {assetInfo.Name} ({GBConverter.FormatSizeFromBytes(assetInfo.Length)})");

            _ = await AdbManager.PushFilesystemItemAsync(file, obbDirectory, token: token);
        }
    }

    private async Task<PackageToolingPaths?> PreflightCheckAsync(RenameBatchRequest batch, CancellationToken token)
    {
        string? replacementName = batch.UserRenameConfiguration.ApkNameReplacement;

        if (string.IsNullOrWhiteSpace(replacementName))
        {
            _logger.LogError("The replacement APK name cannot be empty. Use 'apkognito' if you don't know what to replace it with.");

            // Other checks don't really matter if the rename name is blank, so just return now
            return null;
        }

        bool ready = false;

        if (!ValidCompanyName(replacementName))
        {
            string fixedName = ApkNameFixerRegex().Replace(replacementName, string.Empty);
            _logger.LogError($"The name '{replacementName}' cannot be used with as the company name of an APK. You can use '{fixedName}' which has all offending characters removed.");
            ready = false;
        }

        _logger.Log("Verifying that Java 8+ and APK tools are installed...");
        JavaVersionInformation? javaVersion = VerifyJavaInstallation(batch);

        if (javaVersion is not null)
        {
            _logger.Log($"Using {javaVersion.JavaType} {javaVersion.Version}");
        }
        else
        {
            ready = false;
        }

        // Checks past here are slower or use IO, so stop here if the session is already invalid
        if (!ready)
        {
            return null;
        }

        PackageToolingPaths toolingPaths = await UtilityInstaller.VerifyToolInstallationsAsync(_logger, token);

        return ready
            ? toolingPaths with { JavaExecutablePath = javaVersion!.JavaPath }
            : null;
    }

    private JavaVersionInformation? VerifyJavaInstallation(RenameBatchRequest batch)
    {
        try
        {
            return JavaVersionCollector.GetVersion(batch.UserRenameConfiguration.SelectedRawJavaVersion);
        }
        catch (JavaVersionCollector.NoJavaInstallationsException noJava)
        {
            FileLogger.LogException(noJava);
            _logger.LogError($"Failed to find a valid JDK/JRE installation!\n" +
                "You can install JDK by navigating to the ADB Configuration page and running the installation quick command.\n" +
                "Alternatively, you can run the command `:install-jdk` in the Console page, or manually install a preferred version.\n");
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        return null;
    }

    private async Task CleanUpTempsAsync(PackageRenameConfiguration renameConfig, PackageRenameState nameState, CancellationToken token = default)
    {
        _logger.LogInformation("Cleaning up...");

        if (renameConfig.ClearTempFilesOnRename)
        {
            _logger.LogDebug("Cleaning temp directory `{AssemblyDirectory}`", nameState.PackageAssemblyDirectory);
            await DirectoryManager.DeleteDirectoryAsync(nameState.PackageAssemblyDirectory, token);
        }

        if (renameConfig.AssetRenameConfiguration?.CopyAssets is false)
        {
            _logger.LogDebug("CopyWhenRenaming disabled, deleting directory `{FullSourceApkPath}`", nameState.SourcePackagePath);

            try
            {
                File.Delete(nameState.SourcePackagePath);

                string obbDirectory = Path.GetDirectoryName(nameState.SourcePackagePath)
                    ?? throw new RenameFailedException("Failed to clean OBB directory ");

                await DirectoryManager.DeleteDirectoryAsync(obbDirectory, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear source APK.");
            }
        }
    }

    private async Task TimeAsync(Func<Task> action, string? tag = "Action")
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            await action();
            sw.Stop();
        }
        finally
        {
            sw.Stop();
            _logger.LogDebug($"{tag}: {sw}");
        }
    }

    private static bool ValidCompanyName(string segment)
    {
        return ApkCompanyCheck().IsMatch(segment);
    }

    private static string GetFormattedTimeDirectory(string sourceApkName)
    {
        // apktool fails if using certain special characters.
        return $"{sourceApkName}_{DateTime.Now.ToString("yyyy-MMMM-dd_h.mm", new System.Globalization.CultureInfo("en-US"))}";
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex ApkNameFixerRegex();

    [GeneratedRegex("[a-zA-Z][a-z0-9_]*")]
    private static partial Regex ApkCompanyCheck();
}
