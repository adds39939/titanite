using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.UI.Components.Launch;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Components.GameConfig;

public partial class GameConfigPanel : LauncherAvailabilityView
{
    private const string LibraryRoute = "/";

    [Inject]
    private IGameConfigurationPresenter Presenter { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IGameConfigurationWatcher Watcher { get; set; } = null!;

    [Parameter]
    public GameId GameId { get; set; }

    private LaunchOptionsEditor? Editor { get; set; }

    private bool _hasChosenSection;

    private bool IsConfirmingSave { get; set; }

    private bool IsChoosingSource { get; set; }

    private bool ResetPending { get; set; }

    private bool LaunchPending { get; set; }

    private GameId _followed;

    protected override async Task OnParametersSetAsync()
    {
        _hasChosenSection = false;

        await Presenter.LoadAsync(GameId);

        Follow(GameId);
    }

    private void Follow(GameId id)
    {
        if (_followed == id)
        {
            return;
        }

        StopFollowing();

        _followed = id;

        if (!id.IsEmpty)
        {
            Watcher.Changed += OnConfigurationChanged;
            Watcher.Follow(id);
        }
    }

    private void StopFollowing()
    {
        if (_followed.IsEmpty)
        {
            return;
        }

        Watcher.Changed -= OnConfigurationChanged;
        Watcher.Drop(_followed);

        _followed = default;
    }

    private void OnConfigurationChanged(GameId id)
    {
        if (id != _followed)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            if (await Presenter.RefreshAsync())
            {
                StateHasChanged();
            }
        });
    }

    public override void Dispose()
    {
        StopFollowing();

        base.Dispose();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (_hasChosenSection || Editor is null)
        {
            return;
        }

        _hasChosenSection = true;

        Editor.SelectFirstConfiguredCategory();
        StateHasChanged();
    }

    private void OnOptionsChanged(LaunchOptions options)
    {
        Presenter.Edit(options);
        Settle();
    }

    private Task OnPresetChosen(string? presetId)
    {
        Settle();

        return Presenter.UsePresetAsync(presetId);
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

    private void AskToCopy()
    {
        Settle();
        IsChoosingSource = true;
    }

    private void CancelCopy() => IsChoosingSource = false;

    private async Task CopyFrom(GameEntry source)
    {
        IsChoosingSource = false;

        await Presenter.CopyFromAsync(source);
    }

    private void Launch()
    {
        ResetPending = false;

        if (!LaunchPending && Presenter.WarnBeforeLaunching())
        {
            LaunchPending = true;

            return;
        }

        LaunchPending = false;

        Presenter.Launch();
    }

    private void OpenInstallDirectory()
    {
        Settle();
        Presenter.OpenInstallDirectory();
    }

    private void OpenPrefixDirectory()
    {
        Settle();
        Presenter.OpenPrefixDirectory();
    }

    private void Settle()
    {
        ResetPending = false;
        LaunchPending = false;
    }

    private void Back() => Navigation.NavigateTo(LibraryRoute);

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && !Presenter.IsSaving && !IsConfirmingSave)
        {
            Back();
        }
    }
}
