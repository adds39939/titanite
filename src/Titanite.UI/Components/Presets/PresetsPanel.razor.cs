using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;
using Titanite.UI.Components.Launch;
using Titanite.UI.Services.Editing;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Components.Presets;

public partial class PresetsPanel : LauncherAvailabilityView
{
    [Inject]
    private IPresetsPresenter Presenter { get; set; } = null!;

    private bool IsConfirmingSave { get; set; }

    private bool IsNaming { get; set; }

    private bool ResetPending { get; set; }

    private bool DeletePending { get; set; }

    [Inject]
    private IUnsavedChanges UnsavedChanges { get; set; } = null!;

    private IDisposable? _unsavedChanges;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _unsavedChanges = UnsavedChanges.Track(() => Presenter.HasChanges);
    }

    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();

        await Presenter.LoadAsync();
    }

    private Task Choose(string id)
    {
        Settle();

        return Presenter.SelectAsync(id);
    }

    private void OnOptionsChanged(LaunchOptions options)
    {
        Presenter.Edit(options);
        Settle();
    }

    private void OnCompatToolChanged(string toolName)
    {
        Presenter.ChooseCompatibilityTool(toolName);
        Settle();
    }

    private void Revert()
    {
        Presenter.Revert();
        Settle();
    }

    private async Task ResetAsync()
    {
        if (!ResetPending)
        {
            ResetPending = true;

            return;
        }

        Settle();

        await Presenter.ResetAsync();
    }

    private async Task DeleteAsync()
    {
        if (!DeletePending)
        {
            DeletePending = true;

            return;
        }

        Settle();

        await Presenter.DeleteAsync();
    }

    private void AskToCreate()
    {
        Settle();
        IsNaming = true;
    }

    private void CancelCreate() => IsNaming = false;

    private async Task CreateAsync(string name)
    {
        IsNaming = false;

        await Presenter.CreateAsync(name);
    }

    private void AskToSave()
    {
        Settle();
        IsConfirmingSave = true;
    }

    private void CancelSave() => IsConfirmingSave = false;

    private async Task SaveAsync()
    {
        await Presenter.SaveAsync();

        IsConfirmingSave = false;
    }

    private void Settle()
    {
        ResetPending = false;
        DeletePending = false;
    }

    public override void Dispose()
    {
        _unsavedChanges?.Dispose();

        base.Dispose();
    }
}
