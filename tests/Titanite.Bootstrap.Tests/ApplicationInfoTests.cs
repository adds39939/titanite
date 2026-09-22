using System.Reflection;

namespace Titanite.Bootstrap.Tests;

public class ApplicationInfoTests
{
    [Fact]
    public void ShowsTheVersionWithoutTheCommitItWasBuiltFrom()
    {
        var version = ApplicationInfo.ReadVersion(typeof(ApplicationInfo).Assembly);

        Assert.DoesNotContain('+', version);
        Assert.StartsWith(
            version,
            typeof(ApplicationInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
    }

    [Fact]
    public void PointsAtTheProjectOnGitHub() =>
        Assert.Equal("github.com", new ApplicationInfo().RepositoryUrl.Host);
}
