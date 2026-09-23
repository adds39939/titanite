using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Settings;
using Titanite.Core.Settings;

namespace Titanite.UI.Components.Preferences;

public partial class InterfaceScalePanel : ComponentBase
{
    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    [Inject]
    private IInterfaceScaler Scaler { get; set; } = null!;

    private int Current { get; set; } = InterfaceScales.Default;

    private bool IsLoading { get; set; } = true;

    private bool IsBusy { get; set; }

    private string? Message { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Current = (await Settings.GetAsync()).InterfaceScale;

        IsLoading = false;
    }

    private static string Describe(int scale) =>
        scale == InterfaceScales.Default ? $"{scale}% (default)" : $"{scale}%";

    private async Task ChooseAsync(int scale)
    {
        if (scale == Current)
        {
            return;
        }

        IsBusy = true;
        Message = null;

        var previous = Current;

        try
        {
            Current = scale;

            await Settings.SaveAsync((await Settings.GetAsync()) with { InterfaceScale = scale });
            await Scaler.ApplyAsync(scale);
        }
        catch (Exception e)
        {
            Current = previous;
            Message = $"The interface scale could not be changed: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
