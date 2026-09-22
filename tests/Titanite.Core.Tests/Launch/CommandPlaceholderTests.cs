using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class CommandPlaceholderTests
{
    [Fact]
    public void SettingTheFirstVariableAddsThePlaceholder()
    {
        var options = new LaunchOptions().SetEnvironment("PROTON_ENABLE_NVAPI", "1");

        Assert.True(options.HasCommandPlaceholder);
        Assert.Equal("PROTON_ENABLE_NVAPI=1 %command%", options.Format());
    }

    [Fact]
    public void AddingTheFirstWrapperAddsThePlaceholder() =>
        Assert.Equal("mangohud %command%", new LaunchOptions().WithWrapperCommand("mangohud", true).Format());

    [Fact]
    public void PinningAnEmptyConfigurationAddsThePlaceholder() =>
        Assert.Equal("taskset -c 0-7 %command%", new LaunchOptions().WithCpuAffinity("0-7").Format());

    [Fact]
    public void BuildingUpFromNothingStaysWellFormed()
    {
        var options = new LaunchOptions()
            .SetEnvironment("PROTON_ENABLE_NVAPI", "1")
            .SetEnvironment("PROTON_ENABLE_NGX_UPDATER", "1")
            .WithWrapperCommand("mangohud", true)
            .WithCpuAffinity("0-7,16-23");

        Assert.Equal(
            "PROTON_ENABLE_NVAPI=1 PROTON_ENABLE_NGX_UPDATER=1 mangohud taskset -c 0-7,16-23 %command%",
            options.Format());
    }

    [Fact]
    public void LeavesADeliberateArgumentOnlyStringAlone()
    {
        var options = LaunchOptions.Parse("-novid -console").SetEnvironment("PROTON_LOG", "1");

        Assert.False(options.HasCommandPlaceholder);
    }

    [Fact]
    public void WarnsWhenSettingsCannotApplyWithoutThePlaceholder()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 -novid"));

        Assert.Contains(warnings, warning => warning.Contains("%command%", StringComparison.Ordinal));
    }

    [Fact]
    public void SaysNothingAboutArgumentsAlone() =>
        Assert.Empty(LaunchOptionsValidator.Validate(LaunchOptions.Parse("-novid -console")));
}
