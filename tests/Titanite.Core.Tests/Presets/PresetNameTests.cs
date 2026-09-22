using Titanite.Core.Presets;

namespace Titanite.Core.Tests.Presets;

public class PresetNameTests
{
    private static readonly Preset[] Existing =
    [
        Preset.Global,
        new() { Id = "a", Name = "Handheld" },
        new() { Id = "b", Name = "Handheld 2" }
    ];

    [Theory]
    [InlineData("  Handheld  ", "Handheld")]
    [InlineData("Handheld", "Handheld")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TrimsWhatSomebodyTyped(string? given, string expected) =>
        Assert.Equal(expected, PresetName.Clean(given));

    [Fact]
    public void CutsANameThatWouldNotFit()
    {
        var clean = PresetName.Clean(new string('a', PresetName.MaximumLength + 20));

        Assert.Equal(PresetName.MaximumLength, clean.Length);
    }

    [Fact]
    public void NoticesANameAlreadyInUse()
    {
        Assert.True(PresetName.IsTaken("Handheld", Existing));
        Assert.True(PresetName.IsTaken("handheld", Existing));
        Assert.False(PresetName.IsTaken("Docked", Existing));
    }

    [Fact]
    public void LeavesAFreeNameAlone() => Assert.Equal("Docked", PresetName.Unique("Docked", Existing));

    [Fact]
    public void CountsPastTheNamesAlreadyThere() =>
        Assert.Equal("Handheld 3", PresetName.Unique("Handheld", Existing));
}
