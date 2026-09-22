using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Core.Proton;
using Titanite.Steam.Library;
using Titanite.Steam.Proton;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class ProtonToolServiceTests : IDisposable
{
    private const uint ExperimentalAppId = 1493710;

    private const uint Rematch = 2138720;

    private readonly string _root = Directory.CreateTempSubdirectory("titanite-proton-").FullName;

    public ProtonToolServiceTests()
    {
        InstallValveTool(ExperimentalAppId, "Proton Experimental", "Proton - Experimental",
            layer: "proton", version: "1785947781 experimental-11.0-20260805");

        InstallValveTool(4183110, "Steam Linux Runtime 4.0", "SteamLinuxRuntime_4",
            layer: "container-runtime", version: "1780000000 4.0");

        InstallGame(Rematch, "REMATCH");

        InstallCustomTool("GE-Proton11-3", displayName: "GE-Proton11-3", layer: "proton",
            version: "1784963766 GE-Proton11-3");

        WriteLog($"[2026-08-09 00:23:22] Registering tool proton_experimental, AppID {ExperimentalAppId}");

        WriteMappings(
            ("0", "proton_experimental", 75),
            (Rematch.ToString(), "GE-Proton11-3", 250));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ProtonToolService CreateService(string? steamRoot = null)
    {
        var locator = FakeSteamInstall.At(steamRoot ?? _root);

        return new ProtonToolService(
            locator,
            new SteamLibraryService(locator, NullLogger<SteamLibraryService>.Instance),
            NullLogger<ProtonToolService>.Instance);
    }

    [Fact]
    public async Task NamesAValveBuildFromSteamsCompatibilityLog()
    {
        var catalogue = await CreateService().GetCatalogueAsync();
        var build = Assert.Single(catalogue.Builds, candidate => candidate.Kind == ProtonBuildKind.Valve);

        Assert.Equal("proton_experimental", build.Name);
        Assert.Equal("Proton Experimental", build.DisplayName);
        Assert.Equal("experimental-11.0-20260805", build.Version);
        Assert.Equal(ExperimentalAppId, build.AppId);
        Assert.False(build.NameIsDerived);
    }

    [Fact]
    public async Task InfersAValveBuildsNameWhenTheLogIsGone()
    {
        File.Delete(Path.Combine(_root, "logs", "compat_log.txt"));

        var catalogue = await CreateService().GetCatalogueAsync();
        var build = Assert.Single(catalogue.Builds, candidate => candidate.Kind == ProtonBuildKind.Valve);

        Assert.Equal("proton_experimental", build.Name);
        Assert.True(build.NameIsDerived);
    }

    [Fact]
    public async Task TakesTheMostRecentRegistrationForABuild()
    {
        WriteLog(
            $"[2026-08-09 00:23:22] Registering tool proton_old, AppID {ExperimentalAppId}",
            "[2026-08-09 00:23:22] Registering tool GE-Proton11-3, AppID 0",
            $"[2026-08-10 11:04:01] Registering tool proton_experimental, AppID {ExperimentalAppId}");

        var catalogue = await CreateService().GetCatalogueAsync();

        Assert.Equal("proton_experimental", catalogue.FindBuild("proton_experimental")?.Name);
    }

    [Fact]
    public async Task TakesACustomBuildsNameFromItsManifestRatherThanItsDirectory()
    {
        Directory.Move(
            Path.Combine(_root, "compatibilitytools.d", "GE-Proton11-3"),
            Path.Combine(_root, "compatibilitytools.d", "renamed-by-hand"));

        var catalogue = await CreateService().GetCatalogueAsync();
        var build = Assert.Single(catalogue.Builds, candidate => candidate.Kind == ProtonBuildKind.Custom);

        Assert.Equal("GE-Proton11-3", build.Name);
        Assert.False(build.NameIsDerived);
        Assert.EndsWith("renamed-by-hand", build.InstallPath);
    }

    [Fact]
    public async Task IgnoresCompatibilityToolsThatAreNotProton()
    {
        InstallCustomTool("Luxtorpeda", displayName: "Luxtorpeda", layer: "luxtorpeda", version: "1 v70");

        var catalogue = await CreateService().GetCatalogueAsync();

        Assert.Equal(["proton_experimental", "GE-Proton11-3"], catalogue.Builds.Select(build => build.Name));
    }

    [Fact]
    public async Task ListsValveBuildsBeforeThoseInstalledByHand()
    {
        var catalogue = await CreateService().GetCatalogueAsync();

        Assert.Equal(
            [ProtonBuildKind.Valve, ProtonBuildKind.Custom],
            catalogue.Builds.Select(build => build.Kind));
    }

    [Fact]
    public async Task ReadsTheDefaultAndPerGameMappings()
    {
        var assignments = await CreateService().GetAssignmentsAsync();

        Assert.Equal("proton_experimental", assignments.Default);
        Assert.Equal("GE-Proton11-3", assignments.For(SteamIds.For(Rematch)));
    }

    [Fact]
    public async Task SkipsAMappingWhoseToolNameWasCleared()
    {
        WriteMappings(("0", "proton_experimental", 75), (Rematch.ToString(), string.Empty, 250));

        var assignments = await CreateService().GetAssignmentsAsync();

        Assert.Null(assignments.For(SteamIds.For(Rematch)));
    }

    [Fact]
    public async Task ReturnsAnEmptyCatalogueWhenSteamIsNotInstalled()
    {
        var locator = FakeSteamInstall.At(null);

        var service = new ProtonToolService(
            locator,
            new SteamLibraryService(locator, NullLogger<SteamLibraryService>.Instance),
            NullLogger<ProtonToolService>.Instance);

        var catalogue = await service.GetCatalogueAsync();
        var assignments = await service.GetAssignmentsAsync();

        Assert.Empty(catalogue.Builds);
        Assert.Empty(assignments.ByGame);
    }

    [Fact]
    public async Task ToleratesAConfigWithNoMappingsAtAll()
    {
        File.WriteAllText(
            Path.Combine(_root, "config", "config.vdf"),
            "\"InstallConfigStore\"\n{\n\t\"Software\"\n\t{\n\t}\n}\n");

        var catalogue = await CreateService().GetCatalogueAsync();
        var assignments = await CreateService().GetAssignmentsAsync();

        Assert.NotEmpty(catalogue.Builds);
        Assert.Empty(assignments.ByGame);
        Assert.Null(assignments.Default);
    }

    [Fact]
    public async Task ReadsWhatABuildHonoursFromItsOwnScript()
    {
        var catalogue = await CreateService().GetCatalogueAsync();
        var ge = catalogue.FindBuild("GE-Proton11-3")!;
        var valve = catalogue.FindBuild("proton_experimental")!;

        Assert.True(ge.Capabilities.Reads("PROTON_DLSS_UPGRADE"));
        Assert.False(valve.Capabilities.Reads("PROTON_DLSS_UPGRADE"));

        Assert.True(ge.Capabilities.Reads("PROTON_LOG"));
        Assert.True(valve.Capabilities.Reads("PROTON_LOG"));
    }

    [Fact]
    public async Task JudgesNothingWhenABuildHasNoScript()
    {
        File.Delete(Path.Combine(_root, "compatibilitytools.d", "GE-Proton11-3", "proton"));

        var catalogue = await CreateService().GetCatalogueAsync();
        var build = catalogue.FindBuild("GE-Proton11-3")!;

        Assert.False(build.Capabilities.IsKnown);
        Assert.Null(build.Capabilities.Reads("PROTON_DLSS_UPGRADE"));
    }

    private void InstallValveTool(uint appId, string name, string installDir, string layer, string version)
    {
        var path = Path.Combine(_root, "steamapps", "common", installDir);

        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "toolmanifest.vdf"),
            $"\"manifest\"\n{{\n  \"version\" \"2\"\n  \"compatmanager_layer_name\" \"{layer}\"\n}}\n");
        File.WriteAllText(Path.Combine(path, "version"), version + "\n");

        WriteScript(path, "PROTON_LOG", "PROTON_NO_ESYNC");
        WriteAppManifest(appId, name, installDir);
    }

    private static void WriteScript(string installPath, params string[] variables) =>
        File.WriteAllText(
            Path.Combine(installPath, "proton"),
            "#!/usr/bin/env python3\n" +
            string.Concat(variables.Select(variable => $"    self.check_environment(\"{variable}\", \"x\")\n")));

    private void InstallGame(uint appId, string installDir)
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps", "common", installDir));

        WriteAppManifest(appId, installDir, installDir);
    }

    private void WriteAppManifest(uint appId, string name, string installDir) =>
        File.WriteAllText(
            Path.Combine(_root, "steamapps", $"appmanifest_{appId}.acf"),
            $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"name\"\t\t\"{name}\"\n" +
            $"\t\"installdir\"\t\t\"{installDir}\"\n\t\"StateFlags\"\t\t\"4\"\n}}\n");

    private void InstallCustomTool(string toolName, string displayName, string layer, string version)
    {
        var path = Path.Combine(_root, "compatibilitytools.d", toolName);

        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "toolmanifest.vdf"),
            $"\"manifest\"\n{{\n  \"version\" \"2\"\n  \"compatmanager_layer_name\" \"{layer}\"\n}}\n");
        File.WriteAllText(Path.Combine(path, "version"), version + "\n");

        WriteScript(path, "PROTON_LOG", "PROTON_NO_ESYNC", "PROTON_DLSS_UPGRADE", "PROTON_ENABLE_HDR");

        File.WriteAllText(Path.Combine(path, "compatibilitytool.vdf"),
            "\"compatibilitytools\"\n{\n  \"compat_tools\"\n  {\n" +
            $"    \"{toolName}\"\n    {{\n      \"install_path\" \".\"\n" +
            $"      \"display_name\" \"{displayName}\"\n" +
            "      \"from_oslist\"  \"windows\"\n      \"to_oslist\"    \"linux\"\n    }\n  }\n}\n");
    }

    private void WriteLog(params string[] lines)
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        File.WriteAllLines(Path.Combine(_root, "logs", "compat_log.txt"), lines);
    }

    private void WriteMappings(params (string AppId, string ToolName, int Priority)[] mappings)
    {
        var entries = string.Concat(mappings.Select(mapping =>
            $"\t\t\t\t\t\"{mapping.AppId}\"\n\t\t\t\t\t{{\n" +
            $"\t\t\t\t\t\t\"name\"\t\t\"{mapping.ToolName}\"\n" +
            "\t\t\t\t\t\t\"config\"\t\t\"\"\n" +
            $"\t\t\t\t\t\t\"priority\"\t\t\"{mapping.Priority}\"\n\t\t\t\t\t}}\n"));

        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(
            Path.Combine(_root, "config", "config.vdf"),
            "\"InstallConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
            "\t\t\t\t\"CompatToolMapping\"\n\t\t\t\t{\n" + entries +
            "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n");
    }
}
