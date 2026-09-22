using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Platform.Desktop;

namespace Titanite.Platform.Tests;

public class XdgBrowserServiceTests
{
    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("steam://rungameid/440")]
    public void OpensNothingButAWebAddress(string address) =>
        Assert.False(new XdgBrowserService(NullLogger<XdgBrowserService>.Instance).Open(new Uri(address)));
}
