using System.Text.RegularExpressions;
using APKognito.ApkLib;
using APKognito.ApkLib.Configuration;
using APKognito.Base.MVVM;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using RenameProgressReporter = System.IProgress<APKognito.ApkMod.RenameProgressReport>;

namespace APKognito.ApkMod;

public sealed record RenameJobRequest(
    string[] SourcePackages,
    UserRenameConfiguration UserConfig,
    AdvancedApkRenameSettings AdvancedConfig,
    string OutputDirectory,
    CancellationToken Token
);

public sealed record RenameProgressReport(
    string CurrentPackage,
    int JobNumber,
    int TotalJobs
);

public interface IPackageRenameService
{
    Task RenameBatchAsync(
        RenameJobRequest jobRequest,
        IViewLogger logger,
        RenameProgressReporter packageDataUpdates,
        IProgress<ProgressInfo> packageRenameUpdates
    );
}

public sealed class PackageRenameService : IPackageRenameService
{
    public async Task RenameBatchAsync(
        RenameJobRequest jobRequest,
        IViewLogger logger,
        RenameProgressReporter packageDataUpdates,
        IProgress<ProgressInfo> packageRenameUpdates)
    {
        ArgumentNullException.ThrowIfNull(jobRequest);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(packageDataUpdates);
        ArgumentNullException.ThrowIfNull(packageRenameUpdates);

        using RenameOrchestrator renamer = CreateRenameOrchestrator(logger);
        await renamer.RunBatchAsync(jobRequest, packageDataUpdates, packageRenameUpdates);
    }

    private static RenameOrchestrator CreateRenameOrchestrator(IViewLogger logger)
    {
        return new RenameOrchestrator(logger);
    }
}

public sealed partial class RenameOrchestrator : IDisposable
{
    private readonly IViewLogger _logger;

    private readonly PackageEditorContext _editorContext;

    public RenameOrchestrator(IViewLogger logger)
    {
        ArgumentNullException.ThrowIfNull(_logger);
        _logger = logger;

        _editorContext = new(,);
    }

    public async Task RunBatchAsync(
        RenameJobRequest jobRequest,
        RenameProgressReporter packageDataUpdates,
        IProgress<ProgressInfo> packageRenameUpdates)
    {
        PreFlightResult environment = await PrepareEnvironmentAsync(jobRequest.UserConfig);
        if (!environment)
        {
            return;
        }

        int jobNumber = 1;
        foreach (string file in jobRequest.SourcePackages)
        {
            RenameJob job = CreateJob(file, environment);

            ReportUpdate();
            PackageRenameResult result = await ProcessPackageAsync(job, packageRenameUpdates, jobRequest.Token);

            jobNumber++;
        }

        void ReportUpdate()
        {
            packageDataUpdates.Report(new RenameProgressReport(
                "[Unknown]",
                jobNumber,
                jobRequest.SourcePackages.Length
            ));
        }
    }

    public async Task<PreFlightResult> PrepareEnvironmentAsync(UserRenameConfiguration config)
    {
        if (!IsValidCompanyName(config.ApkNameReplacement))
        {
            return PreFlightResult.Fail("Invalid company name.");
        }

        var java = JavaVersionCollector.GetVersion(config.SelectedRawJavaVersion);

        var apkToolPath = await UtilityInstaller.VerifyToolInstallationsAsync(_logger);

        return PreFlightResult.Success(new PackageToolingPaths(apkToolPath));
    }

    public async Task<PackageRenameResult> ProcessPackageAsync(RenameJob job, IProgress<ProgressInfo> renameReporter, CancellationToken token)
    {
        try
        {
            DirectoryManager.CreateClaimedDirectory(job.TempDirectory);



            await _apkLib.UnpackAsync(job.SourcePath, job.TempPath, token);

            if (job.)
            {
                await ApplyBootstrapAsync(job, token);
            }
            else
            {
                await ApplyBruteRenameAsync(job, token);
            }

            await _apkLib.BuildAndSignAsync(job, token);

            return PackageRenameResult.Success();
        }
        catch (Exception ex)
        {
            return PackageRenameResult.Error(ex.Message);
        }
    }

    private static RenameJob CreateJob(string file, PreFlightResult preFlightResult)
    {
        return new();
    }

    private static bool IsValidCompanyName(string segment)
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

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private sealed record RenameJob(
        string SourcePackagePath,
        string TempDirectory,
        UserRenameConfiguration UserConfig,
        AdvancedApkRenameSettings AdvancedConfig
    );

    private sealed record PreFlightResult(
        bool IsSuccess,
        string Message,
        Exception? Exception = null
    )
    {
        public static PreFlightResult Success(string message)
        {
            return new(true, message);
        }

        public static PreFlightResult Fail(string message, Exception? ex = null)
        {
            return new(false, message, ex);
        }

        public static implicit operator bool(PreFlightResult result) => result.IsSuccess;
    }
}
