using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Settings;
using Titanite.Core.Settings;
using Titanite.UI.Components.Preferences;

namespace Titanite.UI.Tests.Components.Preferences;

public sealed class InterfaceScalePanelTests : BunitContext
{
    private readonly IAppSettingsService _settings = A.Fake<IAppSettingsService>();

    private readonly IInterfaceScaler _scaler = A.Fake<IInterfaceScaler>();

    private AppSettings _stored = new() { ShowVariableDescriptions = true, InterfaceScale = 125 };

    public InterfaceScalePanelTests()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).ReturnsLazily(() => _stored);
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Invokes((AppSettings settings, CancellationToken _) => _stored = settings);

        Services.AddSingleton(_settings);
        Services.AddSingleton(_scaler);
    }

    [Fact]
    public void ShowsTheStoredScale() =>
        Assert.Equal("125%", Render<InterfaceScalePanel>().Find(".dropdown > button .label").TextContent);

    [Fact]
    public void OffersEverySupportedScale()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.Find(".dropdown > button").Click();

        Assert.Equal(InterfaceScales.Supported.Count, panel.FindAll(".item").Count);
    }

    [Fact]
    public void SavesAndAppliesTheChosenScale()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.Find(".dropdown > button").Click();
        panel.FindAll(".item").Single(item => item.TextContent.Trim() == "150%").Click();

        Assert.Equal(150, _stored.InterfaceScale);
        Assert.Equal("150%", panel.Find(".dropdown > button .label").TextContent);
        A.CallTo(() => _scaler.ApplyAsync(150)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void KeepsTheOtherSettingsWhenTheScaleChanges()
    {
        var panel = Render<InterfaceScalePanel>();

        _stored = _stored with { LibraryView = LibraryViewMode.Grid };

        panel.Find(".dropdown > button").Click();
        panel.FindAll(".item").Single(item => item.TextContent.Trim() == "90%").Click();

        Assert.True(_stored.ShowVariableDescriptions);
        Assert.Equal(LibraryViewMode.Grid, _stored.LibraryView);
    }

    [Fact]
    public void LeavesTheScaleAloneWhenItCannotBeSaved()
    {
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("disk full"));

        var panel = Render<InterfaceScalePanel>();

        panel.Find(".dropdown > button").Click();
        panel.FindAll(".item").Single(item => item.TextContent.Trim() == "150%").Click();

        Assert.Equal("125%", panel.Find(".dropdown > button .label").TextContent);
        Assert.Contains("disk full", panel.Find(".message").TextContent, StringComparison.Ordinal);
        A.CallTo(() => _scaler.ApplyAsync(A<int>._)).MustNotHaveHappened();
    }
}
