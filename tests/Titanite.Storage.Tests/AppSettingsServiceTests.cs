using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Core.Settings;

using Titanite.Storage.Settings;

namespace Titanite.Storage.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-settings-").FullName;

    private string SettingsFile => Path.Combine(_root, "settings.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private AppSettingsService CreateService() =>
        new(TitaniteStorage.At(_root), NullLogger<AppSettingsService>.Instance);

    [Fact]
    public async Task OpensOnTheDefaultsWhenNothingHasBeenStored()
    {
        var settings = await CreateService().GetAsync();

        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    [Fact]
    public async Task RemembersHowTheLibraryWasLeft()
    {
        await CreateService().SaveAsync(new AppSettings
        {
            LibraryView = LibraryViewMode.Grid,
            LibrarySort = LibrarySortOrder.RecentlyPlayed
        });

        var reopened = await CreateService().GetAsync();

        Assert.Equal(LibraryViewMode.Grid, reopened.LibraryView);
        Assert.Equal(LibrarySortOrder.RecentlyPlayed, reopened.LibrarySort);
    }

    [Fact]
    public async Task WritesThePreferencesByName()
    {
        await CreateService().SaveAsync(new AppSettings
        {
            LibraryView = LibraryViewMode.Grid,
            LibrarySort = LibrarySortOrder.RecentlyPlayed
        });

        var written = await File.ReadAllTextAsync(SettingsFile);

        Assert.Contains("\"Grid\"", written, StringComparison.Ordinal);
        Assert.Contains("\"RecentlyPlayed\"", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepsTheRestOfTheSettingsWhenOneChanges()
    {
        await CreateService().SaveAsync(new AppSettings { ShowVariableDescriptions = true });

        var service = CreateService();
        var stored = await service.GetAsync();

        await service.SaveAsync(stored with { LibraryView = LibraryViewMode.Grid });

        var reopened = await CreateService().GetAsync();

        Assert.True(reopened.ShowVariableDescriptions);
        Assert.Equal(LibraryViewMode.Grid, reopened.LibraryView);
    }

    [Fact]
    public async Task ReadsAFileFromBeforeThePreferencesExisted()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, """{ "BackupsToKeep": 4 }""");

        var settings = await CreateService().GetAsync();

        Assert.False(settings.ShowVariableDescriptions);
        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    [Fact]
    public async Task FallsBackToTheDefaultsWhenTheFileCannotBeRead()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, "not json at all");

        var settings = await CreateService().GetAsync();

        Assert.False(settings.ShowVariableDescriptions);
        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
    }

    [Fact]
    public async Task RemembersTheInterfaceScale()
    {
        await CreateService().SaveAsync(new AppSettings { InterfaceScale = 125 });

        Assert.Equal(125, (await CreateService().GetAsync()).InterfaceScale);
    }

    [Fact]
    public async Task OpensAtTheDefaultScaleForAFileFromBeforeScalingExisted()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, """{ "LibraryView": "Grid" }""");

        Assert.Equal(100, (await CreateService().GetAsync()).InterfaceScale);
    }

    [Fact]
    public async Task OpensAtTheDefaultScaleWhenTheStoredOneIsNotOffered()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, """{ "InterfaceScale": 9000 }""");

        Assert.Equal(100, (await CreateService().GetAsync()).InterfaceScale);
    }
}
