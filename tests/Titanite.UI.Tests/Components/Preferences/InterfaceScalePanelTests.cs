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
    public void ShowsTheStoredScale()
    {
        var panel = Render<InterfaceScalePanel>();

        Assert.Equal("125", panel.Find(".slider-input").GetAttribute("value"));
        Assert.Equal("125%", panel.Find(".mark.is-selected").TextContent.Trim());
    }

    [Fact]
    public void SlidesInQuarterStepsFromThreeQuartersToDouble()
    {
        var slider = Render<InterfaceScalePanel>().Find(".slider-input");

        Assert.Equal("75", slider.GetAttribute("min"));
        Assert.Equal("200", slider.GetAttribute("max"));
        Assert.Equal("25", slider.GetAttribute("step"));
    }

    [Fact]
    public void MarksEverySupportedScale()
    {
        var marks = Render<InterfaceScalePanel>().FindAll(".mark").Select(mark => mark.TextContent.Trim());

        Assert.Equal(["75%", "100%", "125%", "150%", "175%", "200%"], marks);
    }

    [Fact]
    public void FollowsTheSliderWithoutApplyingUntilItIsReleased()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.Find(".slider-input").Input("175");

        Assert.Equal("175%", panel.Find(".mark.is-selected").TextContent.Trim());
        Assert.Equal(125, _stored.InterfaceScale);
        A.CallTo(() => _scaler.ApplyAsync(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void SavesAndAppliesTheScaleTheSliderIsReleasedOn()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.Find(".slider-input").Input("150");
        panel.Find(".slider-input").Change("150");

        Assert.Equal(150, _stored.InterfaceScale);
        Assert.Equal("150%", panel.Find(".mark.is-selected").TextContent.Trim());
        A.CallTo(() => _scaler.ApplyAsync(150)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void SavesAndAppliesTheScaleOfAClickedMark()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.FindAll(".mark").Single(mark => mark.TextContent.Trim() == "75%").Click();

        Assert.Equal(75, _stored.InterfaceScale);
        A.CallTo(() => _scaler.ApplyAsync(75)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void IgnoresAScaleThatIsNotOffered()
    {
        var panel = Render<InterfaceScalePanel>();

        panel.Find(".slider-input").Change("110");

        Assert.Equal(125, _stored.InterfaceScale);
        A.CallTo(() => _scaler.ApplyAsync(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void KeepsTheOtherSettingsWhenTheScaleChanges()
    {
        var panel = Render<InterfaceScalePanel>();

        _stored = _stored with { LibraryView = LibraryViewMode.Grid };

        panel.Find(".slider-input").Change("100");

        Assert.True(_stored.ShowVariableDescriptions);
        Assert.Equal(LibraryViewMode.Grid, _stored.LibraryView);
    }

    [Fact]
    public void LeavesTheScaleAloneWhenItCannotBeSaved()
    {
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("disk full"));

        var panel = Render<InterfaceScalePanel>();

        panel.Find(".slider-input").Change("150");

        Assert.Equal("125", panel.Find(".slider-input").GetAttribute("value"));
        Assert.Contains("disk full", panel.Find(".message").TextContent, StringComparison.Ordinal);
        A.CallTo(() => _scaler.ApplyAsync(A<int>._)).MustNotHaveHappened();
    }
}
