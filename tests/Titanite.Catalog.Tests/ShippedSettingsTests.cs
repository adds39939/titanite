using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Core.Launch;
using Titanite.Core.Proton;

namespace Titanite.Catalog.Tests;

public class ShippedSettingsTests
{
    private static readonly SettingCatalog Catalog = new YamlSettingCatalogReader(
        YamlSettingCatalogReader.DefaultDirectory,
        NullLogger<YamlSettingCatalogReader>.Instance).Read();

    private static readonly string[] PresetSettings =
    [
        "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION"
    ];

    [Fact]
    public void EveryShippedFileLoads()
    {
        Assert.NotEmpty(Catalog.Categories);
        Assert.NotEmpty(Catalog.All);

        Assert.All(Catalog.All, definition =>
        {
            Assert.NotEmpty(definition.Variable);
            Assert.NotEmpty(definition.Label);
            Assert.NotNull(Catalog.FindCategory(definition.Category.Id));
        });
    }

    [Fact]
    public void NoVariableIsDeclaredTwice()
    {
        var duplicates = Catalog.All
            .GroupBy(definition => definition.Variable, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void SectionIdentifiersAreUnique()
    {
        var duplicates = Catalog.Categories
            .GroupBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData(SettingCategoryIds.Cpu)]
    [InlineData(SettingCategoryIds.MangoHud)]
    public void KeepsTheSectionsTheApplicationLooksForByName(string id) =>
        Assert.NotNull(Catalog.FindCategory(id));

    [Fact]
    public void OrdersSectionsWithNvidiaFirst() =>
        Assert.Equal("nvidia", Catalog.Categories[0].Id);

    [Theory]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION")]
    public void SuperResolutionRayReconstructionAndFrameGenerationAreAllOffered(string variable)
    {
        var definition = Catalog.Find(variable);

        Assert.NotNull(definition);
        Assert.Equal(SettingKind.Choice, definition.Kind);
    }

    [Fact]
    public void EveryPresetOverrideOffersTheSameValues()
    {
        var choices = PresetSettings.Select(variable => Catalog.Find(variable)!.Choices).ToList();

        Assert.All(choices, choice => Assert.Equal(choices[0], choice));
    }

    [Fact]
    public void OffersEveryLetterFromAToZ()
    {
        var choices = Catalog.Find(PresetSettings[0])!.Choices;

        Assert.All(
            Enumerable.Range('A', 26).Select(letter => $"RENDER_PRESET_{(char)letter}"),
            preset => Assert.Contains(preset, choices));
    }

    [Fact]
    public void SpellsDefaultAndLatestExactlyAsTheDriverTableDoes()
    {
        var choices = Catalog.Find(PresetSettings[0])!.Choices;

        Assert.Contains("RENDER_PRESET_Default", choices);
        Assert.Contains("RENDER_PRESET_Latest", choices);
        Assert.DoesNotContain("RENDER_PRESET_DEFAULT", choices);
        Assert.DoesNotContain("RENDER_PRESET_LATEST", choices);
    }

    [Fact]
    public void OffersNothingBeyondTheDriverTable() =>
        Assert.Equal(28, Catalog.Find(PresetSettings[0])!.Choices.Count);

    [Fact]
    public void KeepsTheCompoundToggleValue()
    {
        var definition = Catalog.Find("DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS")!;

        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("DLSSIndicator=1024", definition.OnValue);
    }

    [Fact]
    public void ReadsMangoHudsOptionsFromItsFile()
    {
        var compound = Catalog.Find("MANGOHUD_CONFIG")!.Compound;

        Assert.NotNull(compound);
        Assert.Equal([",", "="], new[] { compound.Separator, compound.Assignment });
        Assert.Equal(["Frame limiting", "Metrics", "Appearance"], compound.Groups.Select(group => group.Name));

        Assert.Equal(SettingKind.Toggle, compound.Find("fps")!.Kind);
        Assert.Equal(SettingKind.Text, compound.Find("fps_limit")!.Kind);
        Assert.Equal(["early", "late"], compound.Find("fps_limit_method")!.Choices);
    }

    [Fact]
    public void ReadsTheDxvkOverlayTheSameWay()
    {
        var compound = Catalog.Find("DXVK_HUD")!.Compound;

        Assert.NotNull(compound);
        Assert.NotNull(compound.Find("fps"));
        Assert.Equal(SettingKind.Text, compound.Find("scale")!.Kind);
    }

    [Fact]
    public void NoCompoundDeclaresAnOptionTwice()
    {
        var duplicates = Catalog.All
            .Where(definition => definition.Compound is not null)
            .SelectMany(definition => definition.Compound!.AllOptions
                .GroupBy(option => option.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{definition.Variable}.{group.Key}"));

        Assert.Empty(duplicates);
    }

    [Fact]
    public void ShipsGamescopeAsACommandRatherThanVariables()
    {
        var command = Catalog.FindCategory("gamescope")?.Command;

        Assert.NotNull(command);
        Assert.Equal("gamescope", command.Command);
        Assert.Equal("--", command.Terminator);
        Assert.NotEmpty(command.AllFlags);
    }

    [Theory]
    [InlineData("-W", SettingKind.Number)]
    [InlineData("-H", SettingKind.Number)]
    [InlineData("-r", SettingKind.Number)]
    [InlineData("-f", SettingKind.Toggle)]
    [InlineData("--adaptive-sync", SettingKind.Toggle)]
    [InlineData("--hdr-enabled", SettingKind.Toggle)]
    [InlineData("--hdr-itm-enabled", SettingKind.Toggle)]
    [InlineData("--hdr-itm-sdr-nits", SettingKind.Number)]
    [InlineData("--hdr-itm-target-nits", SettingKind.Number)]
    [InlineData("--sdr-gamut-wideness", SettingKind.Text)]
    [InlineData("--mangoapp", SettingKind.Toggle)]
    public void OffersTheGamescopeFlagsWorthReachingFor(string flag, SettingKind kind)
    {
        var found = GamescopeFlag(flag);

        Assert.NotNull(found);
        Assert.Equal(kind, found.Kind);
        Assert.NotEmpty(found.Label);
    }

    [Theory]
    [InlineData("-W", "--output-width")]
    [InlineData("-H", "--output-height")]
    [InlineData("-r", "--nested-refresh")]
    [InlineData("-f", "--fullscreen")]
    [InlineData("-S", "--scaler")]
    [InlineData("-F", "--filter")]
    public void RecognisesBothSpellingsOfAGamescopeFlag(string flag, string alias) =>
        Assert.Contains(alias, GamescopeFlag(flag)!.Aliases);

    [Fact]
    public void NoGamescopeFlagIsDeclaredTwice()
    {
        var duplicates = Gamescope.AllFlags
            .SelectMany(flag => flag.Spellings)
            .GroupBy(spelling => spelling, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData("ENABLE_GAMESCOPE_WSI")]
    [InlineData("DISABLE_GAMESCOPE_WSI")]
    public void PlacesTheWaylandLayerVariablesInTheGamescopeSection(string variable)
    {
        var definition = Catalog.Find(variable);

        Assert.NotNull(definition);
        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("gamescope", definition.Category.Id);
    }

    private static CommandDefinition Gamescope => Catalog.FindCategory("gamescope")!.Command!;

    private static CommandFlagDefinition? GamescopeFlag(string flag) =>
        Gamescope.AllFlags.FirstOrDefault(candidate =>
            string.Equals(candidate.Flag, flag, StringComparison.Ordinal));

    [Theory]
    [InlineData("PROTON_NO_NTSYNC")]
    [InlineData("PROTON_USE_WRITECOPY")]
    [InlineData("PROTON_WAYLAND_MONITOR")]
    public void RestrictsWhatOnlyTheGeFamilyReads(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^GE-Proton"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);
    }

    [Theory]
    [InlineData("PROTON_DLSS_UPGRADE")]
    [InlineData("PROTON_DLSS_INDICATOR")]
    [InlineData("PROTON_ENABLE_HDR")]
    [InlineData("PROTON_ENABLE_WAYLAND")]
    [InlineData("PROTON_FSR4_UPGRADE")]
    [InlineData("PROTON_XESS_UPGRADE")]
    [InlineData("PROTON_USE_OPTISCALER")]
    [InlineData("PROTON_NVIDIA_LIBS")]
    public void RestrictsWhatBothCommunityFamiliesReadToBothOfThem(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^GE-Proton", "^(proton-)?cachyos"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);

        Assert.True(definition.AppliesTo(Build("GE-Proton11-6-x86_64", "GE-Proton11-6")));
        Assert.True(definition.AppliesTo(Build("proton-cachyos-11.0-20260703-slr-x86_64_v3", "cachyos-11.0-20260703-slr")));
        Assert.False(definition.AppliesTo(Build("proton_experimental", "experimental-11.0-20260826-x86_64")));
    }

    [Theory]
    [InlineData("PROTON_DXVK_SAREK")]
    [InlineData("PROTON_DXVK_LOWLATENCY")]
    [InlineData("PROTON_VKD3D_LOWLATENCY")]
    [InlineData("PROTON_USE_PIPEWIRE")]
    [InlineData("PROTON_ENABLE_MEDIACONV")]
    public void RestrictsWhatOnlyCachyosReads(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^(proton-)?cachyos"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);

        Assert.True(definition.AppliesTo(Build("proton-cachyos-11.0-20260703-slr-x86_64_v3", "cachyos-11.0-20260703-slr")));
        Assert.False(definition.AppliesTo(Build("GE-Proton11-6-x86_64", "GE-Proton11-6")));
    }

    [Theory]
    [InlineData("nvidia", "DLSS")]
    [InlineData("graphics", "Renderer")]
    [InlineData("compatibility", "Memory")]
    [InlineData("diagnostics", "Renderers")]
    public void GroupsTheLongSectionsUnderHeadings(string section, string heading)
    {
        var groups = Catalog.GroupsIn(Catalog.FindCategory(section)!);

        Assert.Contains(heading, groups.Select(group => group.Name));
        Assert.All(groups, group => Assert.NotEmpty(group.Settings));
    }

    [Fact]
    public void EveryGroupedSettingIsStillTheSectionsOwnListInOrder() =>
        Assert.All(Catalog.Categories, category => Assert.Equal(
            Catalog.In(category),
            Catalog.GroupsIn(category).SelectMany(group => group.Settings)));

    private static ProtonBuild Build(string name, string version) => new()
    {
        Name = name,
        DisplayName = name,
        InstallPath = $"/tmp/{name}",
        Kind = ProtonBuildKind.Custom,
        Version = version
    };

    [Theory]
    [InlineData("PROTON_LOG")]
    [InlineData("PROTON_NO_ESYNC")]
    [InlineData("PROTON_NO_FSYNC")]
    [InlineData("PROTON_FORCE_LARGE_ADDRESS_AWARE")]
    [InlineData("PROTON_CPU_TOPOLOGY")]
    [InlineData("PROTON_DISABLE_NVAPI")]
    [InlineData("PROTON_LIMIT_RESOLUTIONS")]
    [InlineData("PROTON_SET_GAME_DRIVE")]
    [InlineData("PROTON_DISABLE_HIDRAW")]
    public void LeavesWhatEveryBuildReadsAlone(string variable) =>
        Assert.Empty(Catalog.Find(variable)!.ProtonBuilds);

    [Theory]
    [InlineData("PROTON_ENABLE_NVAPI")]
    [InlineData("PROTON_ENABLE_NGX_UPDATER")]
    public void DoesNotGuessAtSettingsItCannotPlace(string variable) =>
        Assert.False(Catalog.Find(variable)!.RestrictToProtonBuild);

    [Fact]
    public void RestrictsNothingItCannotCheck() =>
        Assert.DoesNotContain(
            Catalog.All.Where(definition => definition.RestrictToProtonBuild),
            definition => !definition.Variable.StartsWith("PROTON_", StringComparison.Ordinal));

    [Theory]
    [InlineData("PROTON_USE_WAYLAND", "PROTON_ENABLE_WAYLAND")]
    [InlineData("PROTON_USE_HDR", "PROTON_ENABLE_HDR")]
    public void HoldsBackTheOlderSpellingOfASwitchListedTwice(string older, string current)
    {
        var superseded = Catalog.Find(older)!;
        var listed = Catalog.Find(current)!;

        Assert.True(superseded.HideUnlessSet);
        Assert.False(listed.HideUnlessSet);

        Assert.Equal(listed.Category.Id, superseded.Category.Id);
        Assert.Equal(listed.Kind, superseded.Kind);

        Assert.NotEmpty(superseded.ProtonBuilds);
        Assert.All(superseded.ProtonBuilds, pattern => Assert.Contains(pattern, listed.ProtonBuilds));
    }

    [Fact]
    public void LetsOnlyThePreloadListBeSetEmpty()
    {
        var definition = Assert.Single(Catalog.All, definition => definition.AllowEmpty);

        Assert.Equal("LD_PRELOAD", definition.Variable);
        Assert.Equal(SettingKind.Text, definition.Kind);
        Assert.Equal("steam", definition.Category.Id);
    }

    [Fact]
    public void OrdersSteamJustAheadOfCompatibility()
    {
        var ids = Catalog.Categories.Select(category => category.Id).ToList();

        Assert.Equal(ids.IndexOf("compatibility") - 1, ids.IndexOf("steam"));
    }

    [Fact]
    public void HidesNothingElseUntilItIsSet() =>
        Assert.Equal(
            ["PROTON_USE_HDR", "PROTON_USE_WAYLAND"],
            Catalog.All
                .Where(definition => definition.HideUnlessSet)
                .Select(definition => definition.Variable)
                .Order(StringComparer.Ordinal));
}
