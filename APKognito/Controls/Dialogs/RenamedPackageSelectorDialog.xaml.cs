using System.Collections.ObjectModel;
using System.IO;
using APKognito.ApkMod;
using APKognito.Base;
using APKognito.Configurations;
using APKognito.Helpers;
using APKognito.Utilities;
using Wpf.Ui;

namespace APKognito.Controls.Dialogs;

/// <summary>
/// Interaction logic for RenamedPackageSelector.xaml
/// </summary>
public partial class RenamedPackageSelector : IDialogResult<RenamedPackageListing>
{
    private readonly ConfigurationFactory _configFactory;

    public RenamedPackageSelectorViewModel ViewModel { get; private set; }

    public RenamedPackageListing DialogResult => (RenamedPackageListing)MetadataPresenter.SelectedItem;

    public RenamedPackageSelector()
    {
        InitializeComponent();
        ViewModel = null!;
        _configFactory = null!;
    }

    public RenamedPackageSelector(ConfigurationFactory configFactory, IContentDialogService dialogHost)
        : base(dialogHost.GetDialogHostEx())
    {
        DataContext = this;
        ViewModel = new();
        _configFactory = configFactory;

        InitializeComponent();
    }

    protected override async void OnLoaded()
    {
        await RefreshMetadataListAsync();
    }

    public async Task RefreshMetadataListAsync()
    {
        string outputDirectory = _configFactory.GetConfig<Configurations.ConfigModels.UserRenameConfiguration>().ApkOutputDirectory;

        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return;
        }

        List<RenamedPackageListing> found = [];

        foreach (string directory in Directory.EnumerateDirectories(outputDirectory))
        {
            try
            {
                if (!DirectoryManager.TryGetClaimFile(directory, out string? claimFile))
                {
                    continue;
                }

                RenamedPackageMetadata? loadedMetadata = MetadataManager.LoadMetadata(claimFile);

                if (loadedMetadata is null)
                {
                    continue;
                }

                ulong assetsSize = loadedMetadata.RelativeAssetsPath is not null
                    ? await DirectoryManager.GetDirectorySizeAsync(Path.GetFullPath(Path.Combine(directory, loadedMetadata.RelativeAssetsPath)))
                    : 0;

                var foundMetadata = new RenamedPackageListing(
                    Path.Combine(directory, $"{loadedMetadata.PackageName}.apk"),
                    assetsSize,
                    loadedMetadata
                );

                found.Add(foundMetadata);
            }
            catch (Exception ex)
            {
                FileLogger.LogException(ex);
            }
        }

        if (found.Count is 0)
        {
            ViewModel.HideListView = true;
            return;
        }

        foreach (RenamedPackageListing item in found)
        {
            ViewModel.FoundPackages.Add(item);
        }
    }

    public partial class RenamedPackageSelectorViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<RenamedPackageListing> FoundPackages { get; set; } = [];

        [ObservableProperty]
        public partial bool HideListView { get; set; } = false;
    }
}

public record RenamedPackageListing(
    string PackagePath,
    ulong AssetsSize,
    RenamedPackageMetadata Metadata
)
{
    public string FormattedAssetsSize => GBConverter.FormatSizeFromBytes(AssetsSize);
}
