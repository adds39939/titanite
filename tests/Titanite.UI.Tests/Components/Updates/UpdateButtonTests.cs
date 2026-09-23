using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Updates;
using Titanite.UI.Components.Updates;
using Titanite.UI.Services.Editing;

namespace Titanite.UI.Tests.Components.Updates;

public sealed class UpdateButtonTests : BunitContext
{
    private static readonly AppUpdate Update =
        new("0.2.0", "Titanite-0.2.0-x86_64.flatpak", new Uri("https://example.com/bundle"));

    private readonly IAppUpdater _updater = A.Fake<IAppUpdater>();

    private readonly IAppLifetime _lifetime = A.Fake<IAppLifetime>();

    private readonly IUnsavedChanges _unsaved = A.Fake<IUnsavedChanges>();

    public UpdateButtonTests()
    {
        Services.AddSingleton(_updater);
        Services.AddSingleton(_lifetime);
        Services.AddSingleton(_unsaved);
    }

    [Fact]
    public void StaysHiddenWhileUpToDate()
    {
        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns((AppUpdate?)null);

        var button = Render<UpdateButton>();

        Assert.Empty(button.Markup.Trim());
    }

    [Fact]
    public void OffersTheNewVersionAsAnIconWithoutInstallingIt()
    {
        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);

        var button = Render<UpdateButton>();

        var offer = button.Find("button[aria-label='Update Titanite to v0.2.0']");

        Assert.NotNull(offer.QuerySelector("svg"));
        Assert.Empty(offer.TextContent.Trim());
        A.CallTo(() => _updater.InstallAsync(A<AppUpdate>._, A<IProgress<double>?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void ShowsTheDownloadWhileItRuns()
    {
        var install = new TaskCompletionSource<bool>();
        IProgress<double>? reported = null;

        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._))
            .ReturnsLazily((AppUpdate _, IProgress<double>? progress, CancellationToken _) =>
            {
                reported = progress;

                return install.Task;
            });

        var button = Render<UpdateButton>();

        button.Find("button").Click();

        Assert.True(button.Find("button").HasAttribute("disabled"));
        Assert.Equal("Downloading Titanite v0.2.0", button.Find("button").GetAttribute("title"));

        reported!.Report(0.425);

        button.WaitForAssertion(() =>
            Assert.Equal("Downloading Titanite v0.2.0 (42%)", button.Find("button").GetAttribute("title")));

        reported.Report(1.0);

        button.WaitForAssertion(() =>
            Assert.Equal("Installing Titanite v0.2.0", button.Find("button").GetAttribute("title")));
    }

    [Fact]
    public void SpinsWhileTheUpdateRuns()
    {
        var install = new TaskCompletionSource<bool>();

        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._))
            .Returns(install.Task);

        var button = Render<UpdateButton>();

        Assert.Empty(button.FindAll("button svg path"));

        button.Find("button").Click();

        Assert.Single(button.FindAll("button svg path"));

        install.SetResult(false);

        button.WaitForAssertion(() => Assert.Empty(button.FindAll("button svg path")));
    }

    [Fact]
    public void AsksForARestartOnceInstalled()
    {
        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._)).Returns(true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();

        var done = button.Find("button[aria-label='Restart Titanite to use v0.2.0']");

        Assert.True(done.HasAttribute("disabled"));
        Assert.Empty(done.TextContent.Trim());
    }

    [Fact]
    public void LetsAFailedUpdateBeTriedAgain()
    {
        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._)).Returns(false);

        var button = Render<UpdateButton>();

        button.Find("button").Click();
        button.Find("button[aria-label='Updating to v0.2.0 failed. Click to try again']").Click();

        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public void RestartsOnceTheUpdateIsInstalled()
    {
        Offer(installs: true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();

        A.CallTo(() => _lifetime.Restart()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void DoesNotRestartWhenTheUpdateFailed()
    {
        Offer(installs: false);

        var button = Render<UpdateButton>();

        button.Find("button").Click();

        A.CallTo(() => _lifetime.Restart()).MustNotHaveHappened();
    }

    [Fact]
    public void WarnsBeforeUpdatingOverUnsavedChanges()
    {
        Offer(installs: true);
        A.CallTo(() => _unsaved.Any).Returns(true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();

        Assert.Equal("Update and lose unsaved changes?", button.Find("[role='dialog'] .title").TextContent);
        A.CallTo(() => _updater.InstallAsync(A<AppUpdate>._, A<IProgress<double>?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void LeavesUnsavedChangesAloneWhenCancelled()
    {
        Offer(installs: true);
        A.CallTo(() => _unsaved.Any).Returns(true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();
        button.FindAll("[role='dialog'] button").Single(action => action.TextContent.Trim() == "Cancel").Click();

        Assert.Empty(button.FindAll("[role='dialog']"));
        A.CallTo(() => _updater.InstallAsync(A<AppUpdate>._, A<IProgress<double>?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void UpdatesAndRestartsWhenTheChangesMayBeLost()
    {
        Offer(installs: true);
        A.CallTo(() => _unsaved.Any).Returns(true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();
        button.FindAll("[role='dialog'] button").Single(action => action.TextContent.Trim() == "Update anyway").Click();

        Assert.Empty(button.FindAll("[role='dialog']"));
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _lifetime.Restart()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void WaitsForARestartWhenChangesWereMadeDuringTheUpdate()
    {
        var install = new TaskCompletionSource<bool>();

        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._))
            .Returns(install.Task);

        var button = Render<UpdateButton>();

        button.Find("button").Click();
        A.CallTo(() => _unsaved.Any).Returns(true);
        install.SetResult(true);

        button.WaitForAssertion(() =>
            Assert.Equal("Restart Titanite to use v0.2.0", button.Find("button").GetAttribute("title")));
        A.CallTo(() => _lifetime.Restart()).MustNotHaveHappened();
    }

    [Fact]
    public void ClosesTheWarningWithEscape()
    {
        Offer(installs: true);
        A.CallTo(() => _unsaved.Any).Returns(true);

        var button = Render<UpdateButton>();

        button.Find("button").Click();
        button.Find(".backdrop").KeyDown("Escape");

        Assert.Empty(button.FindAll("[role='dialog']"));
    }

    private void Offer(bool installs)
    {
        A.CallTo(() => _updater.CheckAsync(A<CancellationToken>._)).Returns(Update);
        A.CallTo(() => _updater.InstallAsync(Update, A<IProgress<double>?>._, A<CancellationToken>._)).Returns(installs);
    }
}
