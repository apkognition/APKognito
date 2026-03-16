using APKognito.Base;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace APKognito.Services;

public interface IDialogDispatcher
{
    DialogProxy<TDialog> For<TDialog>() where TDialog : ContentDialog;

    Task<TResult?> ShowDialogAsync<TResult>(IDialogResult<TResult> dialog, CancellationToken token = default)
        where TResult : class;

    Task<TResult?> ShowDialogAsync<TDialog, TResult>(CancellationToken token = default)
        where TDialog : ContentDialog, IDialogResult<TResult>
        where TResult : class;

    ContentDialogHost? GetContentDialogHost();
}

public sealed class DialogDispatcher(IContentDialogService _dialogService, IServiceProvider _serviceProvider) : IDialogDispatcher
{
    public async Task<TResult?> ShowDialogAsync<TResult>(IDialogResult<TResult> dialog, CancellationToken token = default)
        where TResult : class
    {
        if (dialog is not ContentDialog uiDialog)
        {
            ThrowNotContentDialogException();
            return null; // Just to make the compiler happy
        }

        ContentDialogResult result = await _dialogService.ShowAsync(uiDialog, token);

        return result is ContentDialogResult.Primary
            ? dialog.DialogResult
            : null;
    }

    public async Task<TResult?> ShowDialogAsync<TDialog, TResult>(CancellationToken token = default)
        where TDialog : ContentDialog, IDialogResult<TResult>
        where TResult : class
    {
        TDialog dialog = _serviceProvider.GetRequiredService<TDialog>();

        ContentDialogResult result = await _dialogService.ShowAsync(dialog, token);

        return result is ContentDialogResult.Primary
            ? dialog.DialogResult
            : null;
    }

    public DialogProxy<TDialog> For<TDialog>() where TDialog : ContentDialog
    {
        return new(_serviceProvider, _dialogService);
    }

    public ContentDialogHost? GetContentDialogHost()
    {
        return _dialogService.GetDialogHostEx();
    }

    [DoesNotReturn]
    private static void ThrowNotContentDialogException()
    {
        throw new ArgumentException("Dialog must inherit from ContentDialog");
    }
}

public class DialogProxy<TDialog>(IServiceProvider services, IContentDialogService dialogService)
    where TDialog : ContentDialog
{
    public async Task<TResult?> ShowAsync<TResult>(CancellationToken ct = default)
        where TResult : class
    {
        TDialog dialog = services.GetRequiredService<TDialog>();

        if (dialog is not IDialogResult<TResult> resultProvider)
        {
            throw new InvalidOperationException(
                $"{typeof(TDialog).Name} does not implement IDialogResult<{typeof(TResult).Name}>");
        }

        ContentDialogResult result = await dialogService.ShowAsync(dialog, ct);

        return result is ContentDialogResult.Primary
            ? resultProvider.DialogResult
            : default;
    }
}
