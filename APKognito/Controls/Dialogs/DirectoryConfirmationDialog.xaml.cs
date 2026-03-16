using System.IO;
using APKognito.Controls.ViewModels;
using Wpf.Ui.Controls;

namespace APKognito.Controls;

/// <summary>
/// Interaction logic for DirectoryConfirmationDialog.xaml
/// </summary>
public partial class DirectoryConfirmationDialog
{
    public DirectoryConfirmationViewModel ViewModel { get; set; }

    public DirectoryConfirmationDialog()
    {
        // For designer
    }

    public DirectoryConfirmationDialog(ContentDialogHost? dialogHost)
        : this(new(), dialogHost)
    {
    }

    public DirectoryConfirmationDialog(DirectoryConfirmationViewModel viewModel, ContentDialogHost? dialogHost)
        : base(dialogHost)
    {
        DataContext = this;
        ViewModel = viewModel;
        InitializeComponent();
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        switch (button)
        {
            case ContentDialogButton.Close:
                base.OnButtonClick(button);
                break;

            case ContentDialogButton.Primary:
            {
                char[] offending = Path.GetInvalidPathChars();
                if (ViewModel.OutputDirectory.IndexOfAny(offending) is 0)
                {
                    TextBlock.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    _ = DirectorySelectorC.DirectoryTextBox.Focus();
                    return;
                }
            }
            break;
        }

        base.OnButtonClick(button);
    }
}
