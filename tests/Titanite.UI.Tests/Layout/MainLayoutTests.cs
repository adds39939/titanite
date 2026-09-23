using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Desktop;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Updates;
using Titanite.UI.Layout;
using Titanite.UI.Services.Editing;

namespace Titanite.UI.Tests.Layout;

public sealed class MainLayoutTests : BunitContext
{
    private static readonly Uri Repository = new("https://github.com/example/titanite");

    private readonly IBrowserService _browser = A.Fake<IBrowserService>();

    public MainLayoutTests()
    {
        var info = A.Fake<IApplicationInfo>();

        A.CallTo(() => info.Version).Returns("1.2.3");
        A.CallTo(() => info.RepositoryUrl).Returns(Repository);

        Services.AddSingleton(info);
        Services.AddSingleton(_browser);
        Services.AddSingleton(A.Fake<IAppUpdater>());
        Services.AddSingleton(A.Fake<IAppLifetime>());
        Services.AddSingleton(A.Fake<IUnsavedChanges>());
    }

    [Fact]
    public void ShowsTheVersionInTheTitlebar()
    {
        var layout = Render<MainLayout>();

        Assert.Equal("v1.2.3", layout.Find(".titlebar .version").TextContent);
    }

    [Fact]
    public void OpensTheRepositoryInTheBrowser()
    {
        var layout = Render<MainLayout>();

        layout.Find("button[aria-label='Open Titanite on GitHub']").Click();

        A.CallTo(() => _browser.Open(Repository)).MustHaveHappenedOnceExactly();
    }
}
