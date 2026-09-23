using System.Reflection;
using Titanite.Abstractions.Hosting;

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
        Assert.Equal("github.com", new ApplicationInfo(A.Fake<IAppEnvironment>()).RepositoryUrl.Host);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KnowsWhetherTheHostIsInDevelopment(bool development)
    {
        var environment = A.Fake<IAppEnvironment>();

        A.CallTo(() => environment.IsDevelopment).Returns(development);

        Assert.Equal(development, new ApplicationInfo(environment).IsDevelopment);
    }
}
