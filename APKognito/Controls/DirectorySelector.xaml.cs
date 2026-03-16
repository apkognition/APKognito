using System.IO;
using System.Windows.Data;
using APKognito.Utilities;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectorySelector.xaml
/// </summary>
public partial class DirectorySelector
{
    public static readonly DependencyProperty DirectoryPathProperty =
        DependencyProperty.Register(
            nameof(DirectoryPath),
            typeof(string),
            typeof(DirectorySelector),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                    | FrameworkPropertyMetadataOptions.Journal,
                null,
                CoerceDirectoryPath,
                false,
                UpdateSourceTrigger.PropertyChanged)
        );

    public static readonly DependencyProperty SelectingDirectoryProperty =
        DependencyProperty.Register(
            nameof(SelectingDirectory),
            typeof(bool),
            typeof(DirectorySelector),
            new FrameworkPropertyMetadata(true)
        );

    public static readonly DependencyProperty DefaultPathProperty =
        DependencyProperty.Register(
            nameof(DefaultPath),
            typeof(string),
            typeof(DirectorySelector),
            new FrameworkPropertyMetadata(null, DefaultPath_Changed)
        );

    public static readonly DependencyProperty BrowseButtonIconProperty =
        DependencyProperty.Register(
            nameof(BrowseButtonIcon),
            typeof(SymbolIcon),
            typeof(DirectorySelector)
        );

    public string DirectoryPath
    {
        get => (string)GetValue(DirectoryPathProperty);
        set => SetValue(DirectoryPathProperty, value);
    }

    public bool SelectingDirectory
    {
        get => (bool)GetValue(SelectingDirectoryProperty);
        set => SetValue(SelectingDirectoryProperty, value);
    }

    public string DefaultPath
    {
        get => (string)GetValue(DefaultPathProperty);
        set => SetValue(DefaultPathProperty, value);
    }

    public SymbolIcon BrowseButtonIcon
    {
        get => (SymbolIcon)GetValue(BrowseButtonIconProperty);
        set => SetValue(BrowseButtonIconProperty, value);
    }

    public DirectorySelector()
    {
        InitializeComponent();
        BrowseButtonIcon ??= new() { Symbol = SymbolRegular.Folder20 };
    }

    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Used for event handler.")]
    private void DirectoryTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        TextBox tBox;

        switch (sender)
        {
            case TextBox:
                tBox = (TextBox)sender;
                break;

            case DirectorySelector:
                tBox = ((DirectorySelector)sender).DirectoryTextBox;
                break;

            default:
                return;
        }

        App.ForwardKeystrokeToBinding(tBox);
    }

    private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
    {
        string? oldOutput = DirectoryPath;

        if (!Directory.Exists(oldOutput))
        {
            oldOutput = null;
        }

        if (SelectingDirectory)
        {
            string? selectedDirectory = UserSelectDirectory();

            if (selectedDirectory is null)
            {
                return;
            }

            DirectoryPath = selectedDirectory;
            return;
        }

        OpenFileDialog openFileDialog = new()
        {
            Multiselect = false,
            DefaultDirectory = oldOutput ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (openFileDialog.ShowDialog() is false)
        {
            return;
        }

        DirectoryPath = openFileDialog.FileName;
    }

    private static object CoerceDirectoryPath(DependencyObject d, object baseValue)
    {
        return baseValue is string path
            ? VariablePathResolver.Resolve(path)
            : baseValue;
    }

    private static void DefaultPath_Changed(object? sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (DirectorySelector)sender!;
        if (string.IsNullOrEmpty(control.DirectoryPath))
        {
            string resolvedPath = VariablePathResolver.Resolve((string)e.NewValue);
            control.DirectoryPath = resolvedPath;
        }
    }

    public static string? UserSelectDirectory(string? defaultDirectory = null, string title = "Select directory")
    {
        OpenFolderDialog openFolderDialog = new()
        {
            Title = title,
        };

        if (defaultDirectory is not null)
        {
            openFolderDialog.DefaultDirectory = defaultDirectory;
        }

        if (openFolderDialog.ShowDialog() is false)
        {
            return null;
        }

        return openFolderDialog.FolderName;
    }

    public static string? UserSelectFile(string? defaultDirectory = null)
    {
        OpenFileDialog openFileDialog = new();

        if (defaultDirectory is not null)
        {
            openFileDialog.DefaultDirectory = defaultDirectory;
        }

        if (openFileDialog.ShowDialog() is false)
        {
            return null;
        }

        return openFileDialog.FileName;
    }
}
