using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Core.Launch;

namespace Titanite.Catalog.Tests;

public sealed class YamlSettingCatalogReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("titanite-settings-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Write(string name, string contents) =>
        File.WriteAllText(Path.Combine(_directory, name), contents);

    private SettingCatalog Read() =>
        new YamlSettingCatalogReader(_directory, NullLogger<YamlSettingCatalogReader>.Instance).Read();

    [Fact]
    public void ReadsASectionAndItsSettings()
    {
        Write("hdr.yaml", """
            id: hdr
            title: HDR
            order: 2
            settings:
              - variable: DXVK_HDR
                label: Enable HDR in DXVK
                description: Turns on HDR output.
                kind: toggle
            """);

        var catalog = Read();
        var category = Assert.Single(catalog.Categories);
        var definition = Assert.Single(catalog.All);

        Assert.Equal("hdr", category.Id);
        Assert.Equal("HDR", category.Title);
        Assert.Equal(2, category.Order);
        Assert.Equal("DXVK_HDR", definition.Variable);
        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("Turns on HDR output.", definition.Description);
    }

    [Fact]
    public void ReadsSettingsListedUnderHeadings()
    {
        Write("nvidia.yaml", """
            id: nvidia
            groups:
              - name: DLSS
                settings:
                  - variable: PROTON_DLSS_UPGRADE
                    label: Upgrade DLSS libraries
                  - variable: PROTON_DLSS_INDICATOR
                    label: Show the DLSS indicator
              - name: NVAPI
                settings:
                  - variable: PROTON_DISABLE_NVAPI
                    label: Disable NVAPI
            """);

        var catalog = Read();
        var groups = catalog.GroupsIn(catalog.FindCategory("nvidia")!);

        Assert.Equal(["DLSS", "NVAPI"], groups.Select(group => group.Name));
        Assert.Equal(
            ["PROTON_DLSS_UPGRADE", "PROTON_DLSS_INDICATOR"],
            groups[0].Settings.Select(setting => setting.Variable));
    }

    [Fact]
    public void ListsUngroupedSettingsAheadOfTheHeadings()
    {
        Write("hdr.yaml", """
            id: hdr
            settings:
              - variable: DXVK_HDR
                label: Enable HDR in DXVK
            groups:
              - name: Proton
                settings:
                  - variable: PROTON_USE_HDR
                    label: Enable HDR in Proton
            """);

        var catalog = Read();
        var groups = catalog.GroupsIn(catalog.FindCategory("hdr")!);

        Assert.Equal([null, "Proton"], groups.Select(group => group.Name));
        Assert.Equal(["DXVK_HDR"], groups[0].Settings.Select(setting => setting.Variable));
    }

    [Fact]
    public void TreatsAnUnnamedGroupAsUngrouped()
    {
        Write("cpu.yaml", """
            id: cpu
            groups:
              - settings:
                  - variable: PROTON_NO_ESYNC
                    label: Disable esync
            """);

        Assert.Null(Assert.Single(Read().All).Group);
    }

    [Fact]
    public void FallsBackToATextBoxWrittenAsOne()
    {
        Write("graphics.yaml", """
            id: graphics
            settings:
              - variable: DXVK_HUD
                label: DXVK HUD
            """);

        var definition = Assert.Single(Read().All);

        Assert.Equal(SettingKind.Text, definition.Kind);
        Assert.Equal("1", definition.OnValue);
        Assert.Empty(definition.Choices);
        Assert.Empty(definition.ProtonBuilds);
    }

    [Fact]
    public void UsesTheIdAsATitleWhenNoneIsGiven() =>
        Assert.Equal("graphics", ReadOneSection("id: graphics\nsettings: []\n").Title);

    [Theory]
    [InlineData("toggle", SettingKind.Toggle)]
    [InlineData("Choice", SettingKind.Choice)]
    [InlineData("NUMBER", SettingKind.Number)]
    [InlineData("text", SettingKind.Text)]
    public void ReadsEveryKindWhateverTheCasing(string written, SettingKind expected)
    {
        Write("a.yaml", $"""
            id: a
            settings:
              - variable: V
                label: V
                kind: {written}
            """);

        Assert.Equal(expected, Assert.Single(Read().All).Kind);
    }

    [Theory]
    [InlineData("slider")]
    [InlineData("2")]
    public void TreatsAnUnknownKindAsText(string written)
    {
        Write("a.yaml", $"""
            id: a
            settings:
              - variable: V
                label: V
                kind: "{written}"
            """);

        Assert.Equal(SettingKind.Text, Assert.Single(Read().All).Kind);
    }

    [Fact]
    public void ReadsTheValueACompoundToggleWrites()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS
                label: Debug info
                kind: toggle
                on: DLSSIndicator=1024
            """);

        Assert.Equal("DLSSIndicator=1024", Assert.Single(Read().All).OnValue);
    }

    [Fact]
    public void ReadsWhetherASettingMayBeSetEmpty()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: LD_PRELOAD
                label: Preloaded libraries
                allowEmpty: true

              - variable: WINEDEBUG
                label: Wine debug channels
            """);

        var all = Read().All;

        Assert.True(all.Single(definition => definition.Variable == "LD_PRELOAD").AllowEmpty);
        Assert.False(all.Single(definition => definition.Variable == "WINEDEBUG").AllowEmpty);
    }

    [Fact]
    public void ReadsTheBuildsASettingIsDeclaredFor()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: PROTON_DLSS_UPGRADE
                label: Upgrade
                protonBuilds: ["^GE-Proton", "cachyos"]
            """);

        Assert.Equal(["^GE-Proton", "cachyos"], Assert.Single(Read().All).ProtonBuilds);
    }

    [Fact]
    public void ResolvesAListWrittenOnceAndReferredTo()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: FIRST
                label: First
                kind: choice
                choices: &presets [one, two]
              - variable: SECOND
                label: Second
                kind: choice
                choices: *presets
            """);

        var catalog = Read();

        Assert.Equal(["one", "two"], catalog.Find("FIRST")!.Choices);
        Assert.Equal(catalog.Find("FIRST")!.Choices, catalog.Find("SECOND")!.Choices);
    }

    [Fact]
    public void ReadsACompoundVariable()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: MANGOHUD_CONFIG
                label: MangoHud options
                compound:
                  separator: ","
                  assignment: "="
                  groups:
                    - name: Frame limiting
                      options:
                        - key: fps_limit
                          label: Frame rate limit
                          kind: text
                          placeholder: "224"
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound;

        Assert.NotNull(compound);
        Assert.Equal("Frame limiting", Assert.Single(compound.Groups).Name);
        Assert.Equal(SettingKind.Text, compound.Find("fps_limit")!.Kind);
        Assert.Equal("224", compound.Find("fps_limit")!.Placeholder);
    }

    [Fact]
    public void TreatsAnOptionWithNoKindAsAFlag()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - key: fps
                          label: Frame rate
            """);

        Assert.Equal(SettingKind.Toggle, Assert.Single(Read().All).Compound!.Find("fps")!.Kind);
    }

    [Fact]
    public void FallsBackToTheUsualSeparatorAndAssignment()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(",", compound.Separator);
        Assert.Equal("=", compound.Assignment);
    }

    [Fact]
    public void ReadsAFormatThatPacksItselfDifferently()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: WINEDLLOVERRIDES
                label: DLL overrides
                compound:
                  separator: ";"
                  assignment: "="
                  groups:
                    - options:
                        - key: dxgi
                          label: DXGI
                          kind: text
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(";", compound.Separator);
        Assert.Null(Assert.Single(compound.Groups).Name);
    }

    [Fact]
    public void IgnoresACompoundWithNoOptions()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - name: Empty
                      options: []
            """);

        Assert.Null(Assert.Single(Read().All).Compound);
    }

    [Fact]
    public void SkipsAnOptionThatNamesNoKey()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - label: Nothing to set
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(["fps"], compound.AllOptions.Select(option => option.Key));
    }

    [Fact]
    public void ReadsACommandAndItsFlags()
    {
        Write("a.yaml", """
            id: gamescope
            command:
              name: gamescope
              label: Launch through Gamescope
              description: Runs the game inside its own compositor.
              terminator: "--"
              groups:
                - name: Output
                  flags:
                    - flag: "-W"
                      aliases: ["--output-width"]
                      label: Output width
                      kind: number
                      placeholder: "3840"
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        var command = Assert.Single(Read().Categories).Command;

        Assert.NotNull(command);
        Assert.Equal("gamescope", command.Command);
        Assert.Equal("Launch through Gamescope", command.Label);
        Assert.Equal("--", command.Terminator);
        Assert.Equal("Output", Assert.Single(command.Groups).Name);

        var width = command.AllFlags.First();

        Assert.Equal(SettingKind.Number, width.Kind);
        Assert.Equal(["--output-width"], width.Aliases);
        Assert.Equal("3840", width.Placeholder);
    }

    [Fact]
    public void TreatsAFlagWithNoKindAsASwitch()
    {
        Write("a.yaml", """
            id: a
            command:
              name: gamescope
              groups:
                - flags:
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        var flag = Assert.Single(Assert.Single(Read().Categories).Command!.AllFlags);

        Assert.Equal(SettingKind.Toggle, flag.Kind);
        Assert.False(flag.TakesValue);
    }

    [Fact]
    public void LeavesACommandWithNoTerminatorWithoutOne()
    {
        Write("a.yaml", "id: a\ncommand:\n  name: mangohud\nsettings: []\n");

        Assert.Null(Assert.Single(Read().Categories).Command!.Terminator);
    }

    [Fact]
    public void KeepsACommandThatOffersNoFlags()
    {
        Write("a.yaml", "id: a\ncommand:\n  name: gamemoderun\n  label: Launch through GameMode\nsettings: []\n");

        var command = Assert.Single(Read().Categories).Command;

        Assert.NotNull(command);
        Assert.Empty(command.AllFlags);
    }

    [Fact]
    public void SkipsACommandThatNamesNothingToRun()
    {
        Write("a.yaml", "id: a\ncommand:\n  label: Launch through nothing\nsettings: []\n");

        Assert.Null(Assert.Single(Read().Categories).Command);
    }

    [Fact]
    public void SkipsAFlagThatNamesNoFlag()
    {
        Write("a.yaml", """
            id: a
            command:
              name: gamescope
              groups:
                - flags:
                    - label: Nothing to set
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        Assert.Equal(["-f"], Assert.Single(Read().Categories).Command!.AllFlags.Select(flag => flag.Flag));
    }

    [Fact]
    public void LeavesASectionOfVariablesWithNoCommand()
    {
        Write("a.yaml", "id: a\nsettings:\n  - variable: DXVK_HDR\n    label: HDR\n");

        Assert.Null(Assert.Single(Read().Categories).Command);
    }

    [Fact]
    public void OneBrokenFileDoesNotCostTheOthers()
    {
        Write("broken.yaml", "id: broken\nsettings:\n  - variable: [unclosed\n");
        Write("good.yaml", """
            id: good
            settings:
              - variable: DXVK_HDR
                label: HDR
            """);

        var catalog = Read();

        Assert.Equal("good", Assert.Single(catalog.Categories).Id);
        Assert.Equal("DXVK_HDR", Assert.Single(catalog.All).Variable);
    }

    [Fact]
    public void SkipsAFileThatNamesNoSection()
    {
        Write("nameless.yaml", "title: Nameless\nsettings:\n  - variable: V\n    label: V\n");

        Assert.Empty(Read().Categories);
    }

    [Fact]
    public void SkipsASettingThatNamesNoVariable()
    {
        Write("a.yaml", """
            id: a
            settings:
              - label: Nothing to set
              - variable: DXVK_HDR
                label: HDR
            """);

        Assert.Equal("DXVK_HDR", Assert.Single(Read().All).Variable);
    }

    [Fact]
    public void ReadsNothingFromADirectoryThatIsNotThere()
    {
        var catalog = new YamlSettingCatalogReader(
            Path.Combine(_directory, "missing"),
            NullLogger<YamlSettingCatalogReader>.Instance).Read();

        Assert.Empty(catalog.Categories);
        Assert.Empty(catalog.All);
    }

    [Fact]
    public void OrdersSectionsAsTheyAsk()
    {
        Write("aaa.yaml", "id: last\norder: 9\nsettings: []\n");
        Write("zzz.yaml", "id: first\norder: 1\nsettings: []\n");

        Assert.Equal(["first", "last"], Read().Categories.Select(category => category.Id));
    }

    private SettingCategory ReadOneSection(string contents)
    {
        Write("only.yaml", contents);

        return Assert.Single(Read().Categories);
    }
}
