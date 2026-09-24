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

    private int? Sliding { get; set; }

    private int Shown => Sliding ?? Current;

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

    private static int? Parse(ChangeEventArgs args) =>
        int.TryParse(args.Value?.ToString(), out var scale) && InterfaceScales.IsSupported(scale) ? scale : null;

    private string MarkClass(int scale) =>
        scale == Shown ? "mark is-selected" : "mark";

    private void OnSliding(ChangeEventArgs args) => Sliding = Parse(args) ?? Sliding;

    private async Task OnSlidAsync(ChangeEventArgs args)
    {
        var scale = Parse(args);

        Sliding = null;

        if (scale is not null)
        {
            await ChooseAsync(scale.Value);
        }
    }

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
