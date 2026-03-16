using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Base.MVVM;
using APKognito.ConsoleCommands;
using Wpf.Ui.Controls;

namespace APKognito.Controls.Dialogs;

public sealed partial class ConsoleDialog
{
    public ConsoleDialogViewModel ViewModel { get; }

#if DEBUG
    public ConsoleDialog()
    {
        // For designer
    }
#endif

    public ConsoleDialog(ContentPresenter? contentPresenter, string title = "Console", string initialCloseText = "Cancel")
        : base(contentPresenter)
    {
        Title = title;

        DataContext = this;
        ViewModel = new(initialCloseText);

        InitializeComponent();
    }

    public async Task RunInternalCommandAsync(string command)
    {
        await ViewModel.RunInternalAsync(command);
    }

    public async Task<AdbCommandOutput> RunAdbCommandAsync(string command)
    {
        return await ViewModel.RunAdbAsync(command);
    }

    public void Finished(string finishedText = "Close")
    {
        ViewModel.Finished(finishedText);
    }

    public static async Task<ContentDialogResult> RunInternalCommandAsync(
        string command,
        string title,
        string initialCloseText,
        string closeButtonText,
        ContentPresenter? presenter)
    {
        ConsoleDialog dialog = new(presenter, title, initialCloseText);
        Task<ContentDialogResult> showTask = dialog.ShowAsync();
        await dialog.RunInternalCommandAsync(command);
        dialog.Finished(closeButtonText);

        return await showTask;
    }

    private async void ContentDialog_ClosedAsync(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        await ViewModel.CancelCommandCommand.ExecuteAsync(null);
        ViewModel.Dispose();
    }

    public sealed partial class ConsoleDialogViewModel : LoggableObservableObject, IDisposable
    {
        private static CommandHost s_commandHost { get; set; }

        private readonly CancellationTokenSource _cancellationTokenSource;

        [ObservableProperty]
        public partial string InteractionButtonText { get; set; }

        public ConsoleDialogViewModel(string initialCloseText)
        {
            InteractionButtonText = initialCloseText;
            _cancellationTokenSource = new CancellationTokenSource();

            RegisterConsoleHostParams();
        }

        internal async Task RunInternalAsync(string command)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            _ = await s_commandHost.RunCommandAsync(command, this, token);
        }

        internal async Task<AdbCommandOutput> RunAdbAsync(string command)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            return (await AdbManager.LoggedDeviceCommandAsync(command, this, null, true, token))!;
        }

        internal void Finished(string finishedText)
        {
            InteractionButtonText = finishedText;
            WriteGenericLogLine();
            Log("You may now close this dialog.");
        }

        [RelayCommand]
        private async Task OnCancelCommandAsync()
        {
            await _cancellationTokenSource.CancelAsync();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }

        private static void RegisterConsoleHostParams()
        {
            if (s_commandHost is not null)
            {
                return;
            }

            s_commandHost = new();

            CommandParameterProvider paramProvider = s_commandHost.ParameterProvider;
            ConfigurationFactory configFactory = App.GetService<ConfigurationFactory>()!;
            paramProvider.Register(configFactory);
        }
    }
}

internal sealed partial class CommandDialogController : ObservableObject
{
    [ObservableProperty]
    public partial object CurrentContent { get; set; }

    public void SwitchTo(object view)
    {
        CurrentContent = view;
    }
}
