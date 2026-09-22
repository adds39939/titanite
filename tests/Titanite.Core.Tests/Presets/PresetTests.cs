using Titanite.Core.Launch;
using Titanite.Core.Presets;

namespace Titanite.Core.Tests.Presets;

public class PresetTests
{
    [Fact]
    public void GlobalIsTheOnePresetThatCannotBeRemoved()
    {
        Assert.True(Preset.Global.IsGlobal);
        Assert.False(Preset.Global.CanBeRemoved);
        Assert.True(new Preset { Id = "a", Name = "Handheld" }.CanBeRemoved);
    }

    [Fact]
    public void StartsWithNothingSet()
    {
        Assert.True(Preset.Global.Options.IsEmpty);
        Assert.Equal(string.Empty, Preset.Global.CompatibilityTool);
    }

    [Theory]
    [InlineData("global", true)]
    [InlineData("GLOBAL", true)]
    [InlineData("globally", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RecognisesGlobalWhateverTheCasing(string? id, bool expected) =>
        Assert.Equal(expected, PresetId.IsGlobal(id));

    [Fact]
    public void KnowsWhenAGameStillLooksLikeThePreset()
    {
        var preset = new Preset
        {
            Id = "a",
            Name = "Handheld",
            Options = LaunchOptions.Parse("MANGOHUD=1 %command%"),
            CompatibilityTool = "GE-Proton11-3"
        };

        Assert.True(preset.Matches(LaunchOptions.Parse("MANGOHUD=1 %command%"), "GE-Proton11-3"));
        Assert.False(preset.Matches(LaunchOptions.Parse("DXVK_HUD=1 %command%"), "GE-Proton11-3"));
        Assert.False(preset.Matches(LaunchOptions.Parse("MANGOHUD=1 %command%"), string.Empty));
    }
}
