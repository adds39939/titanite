using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class CompoundValueTests
{
    private static readonly CompoundSchema MangoHud = new(",", "=",
    [
        new CompoundOptionGroup("Frame limiting",
        [
            new CompoundOptionDefinition("fps_limit", "Frame rate limit") { Kind = SettingKind.Text },
            new CompoundOptionDefinition("fps_limit_method", "Limiter method") { Kind = SettingKind.Choice }
        ]),
        new CompoundOptionGroup("Metrics",
        [
            new CompoundOptionDefinition("fps", "Frame rate"),
            new CompoundOptionDefinition("gpu_stats", "GPU load"),
            new CompoundOptionDefinition("position", "Position") { Kind = SettingKind.Choice },
            new CompoundOptionDefinition("font_size", "Font size") { Kind = SettingKind.Number }
        ])
    ]);

    private static CompoundValue Parse(string? value) => CompoundValue.Parse(MangoHud, value);

    [Theory]
    [InlineData("")]
    [InlineData("fps")]
    [InlineData("fps_limit=224")]
    [InlineData("fps_limit=224,fps_limit_method=late")]
    [InlineData("fps,frametime,gpu_stats,cpu_stats")]
    [InlineData("fps_limit=0,30,60")]
    [InlineData("position=top-left,font_size=24,round_corners=5")]
    public void FormatRestoresTheOriginalValue(string original) =>
        Assert.Equal(original, Parse(original).Format());

    [Fact]
    public void ReadsTheRealOverwatchConfiguration()
    {
        var value = Parse("fps_limit=224,fps_limit_method=late");

        Assert.Equal("224", value.GetValue("fps_limit"));
        Assert.Equal("late", value.GetValue("fps_limit_method"));
    }

    [Fact]
    public void TreatsABareOptionAsAFlag()
    {
        var value = Parse("fps");

        Assert.True(value.Contains("fps"));
        Assert.Null(value.GetValue("fps"));
    }

    [Fact]
    public void KeepsANumericListWithItsSetting()
    {
        var value = Parse("fps_limit=0,30,60,fps");

        Assert.Equal("0,30,60", value.GetValue("fps_limit"));
        Assert.True(value.Contains("fps"));
        Assert.Empty(value.Unrecognised);
    }

    [Fact]
    public void SettingAnExistingOptionLeavesItWhereItWas() =>
        Assert.Equal("fps,fps_limit=144,gpu_stats",
            Parse("fps,fps_limit=60,gpu_stats").Set("fps_limit", "144").Format());

    [Fact]
    public void SettingANewOptionAppendsIt() =>
        Assert.Equal("fps,position=top-left", Parse("fps").Set("position", "top-left").Format());

    [Fact]
    public void RemovingAnOptionLeavesTheRest() =>
        Assert.Equal("fps,gpu_stats", Parse("fps,fps_limit=60,gpu_stats").Remove("fps_limit").Format());

    [Fact]
    public void SeparatesOptionsWithoutControlsFromThoseWithThem()
    {
        var value = Parse("fps_limit=224,round_corners=5,engine_version");

        Assert.Equal(
            ["round_corners=5", "engine_version"],
            value.Unrecognised.Select(entry => entry.Render(MangoHud)));
    }

    [Fact]
    public void ReplacingTheExtrasKeepsTheRecognisedOptions()
    {
        var value = Parse("fps_limit=224,round_corners=5")
            .ReplaceUnrecognised(Parse("engine_version,wine").Entries);

        Assert.Equal("fps_limit=224,engine_version,wine", value.Format());
    }

    [Fact]
    public void ToleratesUntidySpacingAndEmptyEntries() =>
        Assert.Equal("fps,fps_limit=60", Parse(" fps , , fps_limit=60 ,").Format());

    [Fact]
    public void ReadsAFormatWithItsOwnSeparatorAndAssignment()
    {
        var schema = new CompoundSchema(";", ":",
            [new CompoundOptionGroup(null, [new CompoundOptionDefinition("mode", "Mode") { Kind = SettingKind.Text }])]);

        var value = CompoundValue.Parse(schema, "mode:fast;verbose");

        Assert.Equal("fast", value.GetValue("mode"));
        Assert.True(value.Contains("verbose"));
        Assert.Equal("mode:fast;verbose", value.Format());
    }

    [Fact]
    public void RemovesTheVariableOnceNothingIsLeft() =>
        Assert.True(Parse("fps").Remove("fps").IsEmpty);

    [Fact]
    public void FindsOptionsInAnUnnamedGroup()
    {
        var schema = new CompoundSchema(",", "=",
            [new CompoundOptionGroup(null, [new CompoundOptionDefinition("dxr", "Ray tracing")])]);

        Assert.NotNull(schema.Find("dxr"));
        Assert.Null(schema.Find("nothing"));
    }
    [Fact]
    public void ListsOnlyTheOptionsAValueCarries()
    {
        var schema = new CompoundSchema(
            ",",
            "=",
            [
                new CompoundOptionGroup("Features", [new CompoundOptionDefinition("dxr12", "DXR 1.2"),
                                                     new CompoundOptionDefinition("nodxr", "No ray tracing")]),
                new CompoundOptionGroup("Memory", [new CompoundOptionDefinition("no_upload_hvv", "No HVV")])
            ]);

        var groups = CompoundValue.Parse(schema, "dxr12").GroupsWithValues();

        var group = Assert.Single(groups);

        Assert.Equal("Features", group.Name);
        Assert.Equal(["dxr12"], group.Options.Select(option => option.Key));
    }

    [Fact]
    public void DropsAGroupNoneOfWhoseOptionsAreSet()
    {
        var schema = new CompoundSchema(
            ",",
            "=",
            [
                new CompoundOptionGroup("Features", [new CompoundOptionDefinition("dxr12", "DXR 1.2")]),
                new CompoundOptionGroup("Memory", [new CompoundOptionDefinition("no_upload_hvv", "No HVV")])
            ]);

        Assert.Empty(CompoundValue.Parse(schema, "something_else").GroupsWithValues());
    }
}
