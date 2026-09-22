using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Settings;
using Titanite.Core.Settings;

namespace Titanite.UI.Components.Preferences;

public partial class VariableDescriptionsPanel : ComponentBase
{
    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    private AppSettings Current { get; set; } = new();

    private bool IsLoading { get; set; } = true;

    private bool IsBusy { get; set; }

    private string? Message { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Current = await Settings.GetAsync();

        IsLoading = false;
    }

    private async Task OnToggledAsync(ChangeEventArgs args)
    {
        if (args.Value is not bool show)
        {
            return;
        }

        IsBusy = true;
        Message = null;

        var previous = Current;

        try
        {
            Current = Current with { ShowVariableDescriptions = show };

            await Settings.SaveAsync(Current);
        }
        catch (Exception e)
        {
            Current = previous;
            Message = $"The preference could not be saved: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
