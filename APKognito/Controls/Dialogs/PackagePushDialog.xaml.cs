using System.IO;
using APKognito.Base;
using APKognito.Models;

namespace APKognito.Controls.Dialogs;

/// <summary>
/// Interaction logic for PackagePushDialog.xaml
/// </summary>
public partial class PackagePushDialog : IDialogResult<MinimalPackageInfo>
{
    public PackagePushViewModel ViewModel { get; set; }

    public MinimalPackageInfo? DialogResult => new(ViewModel.PackagePath, ViewModel.AssetsPath);

    public PackagePushDialog()
    {
        ViewModel = null!;
    }

    public PackagePushDialog(IContentDialogService dialogService)
        : base(dialogService.GetDialogHostEx())
    {
        ViewModel = new();
        DataContext = this;

        InitializeComponent();
    }

    public sealed partial class PackagePushViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string PackagePath { get; set; }

        [ObservableProperty]
        public partial string? AssetsPath { get; set; }

        partial void OnPackagePathChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(AssetsPath))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string packageName = Path.GetFileNameWithoutExtension(value);
            string packageParentDirectory = Path.GetDirectoryName(value)!;
            string suspectedAssetsDirectory = Path.Combine(packageParentDirectory, packageName);

            if (Directory.Exists(suspectedAssetsDirectory))
            {
                AssetsPath = suspectedAssetsDirectory;
            }
        }
    }
}
