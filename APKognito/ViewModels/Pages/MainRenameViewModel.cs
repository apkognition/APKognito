using APKognito.Base.MVVM;
using APKognito.Configurations;
using APKognito.Services;

namespace APKognito.ViewModels.Pages;

public sealed partial class MainRenameViewModel : LoggableObservableObject
{
    private readonly ConfigurationFactory _configFactory;
    private readonly IDialogDispatcher _dialogDispatcher;

    public MainRenameViewModel()
    {
        // For designer
    }

    public MainRenameViewModel(
        ConfigurationFactory configurationFactory,
        IDialogDispatcher dialogDispatcher
        )
    {
        _configFactory = configurationFactory;
        _dialogDispatcher = dialogDispatcher;
    }
}
