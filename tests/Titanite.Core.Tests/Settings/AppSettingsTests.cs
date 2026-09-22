using Titanite.Core.Settings;

namespace Titanite.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void OpensOnTheListOrderedByName()
    {
        var settings = new AppSettings();

        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    [Fact]
    public void ReadsAnAbsentPreferenceAsTheDefault()
    {
        Assert.Equal(LibraryViewMode.List, default);
        Assert.Equal(LibrarySortOrder.Name, default);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void FallsBackToTheDefaultViewWhenAskedForOneThatDoesNotExist(int stored) =>
        Assert.Equal(
            LibraryViewMode.List,
            (new AppSettings { LibraryView = (LibraryViewMode)stored }).Sanitised().LibraryView);

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void FallsBackToTheDefaultOrderWhenAskedForOneThatDoesNotExist(int stored) =>
        Assert.Equal(
            LibrarySortOrder.Name,
            (new AppSettings { LibrarySort = (LibrarySortOrder)stored }).Sanitised().LibrarySort);

    [Fact]
    public void KeepsAViewThatDoesExist() =>
        Assert.Equal(
            LibraryViewMode.Grid,
            (new AppSettings { LibraryView = LibraryViewMode.Grid }).Sanitised().LibraryView);

    [Fact]
    public void KeepsAnOrderThatDoesExist() =>
        Assert.Equal(
            LibrarySortOrder.RecentlyPlayed,
            (new AppSettings { LibrarySort = LibrarySortOrder.RecentlyPlayed }).Sanitised().LibrarySort);

    [Fact]
    public void HidesVariableDescriptionsUntilTheyAreAskedFor() =>
        Assert.False(new AppSettings().ShowVariableDescriptions);

    [Fact]
    public void KeepsTheChoiceToShowVariableDescriptions() =>
        Assert.True((new AppSettings { ShowVariableDescriptions = true }).Sanitised().ShowVariableDescriptions);

    [Fact]
    public void HidesNativeGamesAndToolsUntilTheyAreAskedFor()
    {
        var settings = new AppSettings();

        Assert.False(settings.ShowNativeGames);
        Assert.False(settings.ShowTools);
    }

    [Fact]
    public void KeepsTheChoiceToShowNativeGamesAndTools()
    {
        var settings = new AppSettings { ShowNativeGames = true, ShowTools = true }.Sanitised();

        Assert.True(settings.ShowNativeGames);
        Assert.True(settings.ShowTools);
    }

    [Fact]
    public void LeavesEverythingElseAloneWhileCorrectingOneValue()
    {
        var settings = new AppSettings
        {
            LibraryView = (LibraryViewMode)7,
            LibrarySort = LibrarySortOrder.RecentlyPlayed,
            ShowVariableDescriptions = true
        }.Sanitised();

        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.RecentlyPlayed, settings.LibrarySort);
        Assert.True(settings.ShowVariableDescriptions);
    }
}
