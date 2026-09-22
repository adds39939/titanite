namespace Titanite.Core.Launch;

public static class LaunchOptionsValidator
{
    private static readonly string[] WaylandVariables = ["PROTON_ENABLE_WAYLAND", "PROTON_USE_WAYLAND"];

    private static readonly string[] ProtonHdrVariables = ["PROTON_ENABLE_HDR", "PROTON_USE_HDR"];

    private const string MangoHudCommand = "mangohud";

    private const string GamescopeCommand = "gamescope";

    private const string MangoAppFlag = "--mangoapp";

    private static readonly string[] HdrFlags = ["--hdr-enabled"];

    private static readonly string[] InverseToneMappingFlags = ["--hdr-itm-enabled", "--hdr-itm-enable"];

    public static IReadOnlyList<string> Validate(LaunchOptions options)
    {
        var warnings = new List<string>();

        if ((IsAnyOn(options, ProtonHdrVariables) || IsOn(options, "DXVK_HDR")) &&
            !IsAnyOn(options, WaylandVariables))
        {
            warnings.Add(
                "HDR is enabled but the game is not set to run natively on Wayland. HDR does " +
                "nothing through XWayland.");
        }

        if (IsSet(options, "MANGOHUD_CONFIG") &&
            !HasWrapper(options, MangoHudCommand) &&
            !HasArgument(options, MangoAppFlag))
        {
            warnings.Add(
                "MangoHud options are set but the game is not launched through mangohud, so they " +
                "will be ignored.");
        }

        if (HasWrapper(options, GamescopeCommand) && HasWrapper(options, MangoHudCommand))
        {
            warnings.Add(
                "The game is launched through both Gamescope and mangohud. Gamescope's own advice " +
                "is to drop mangohud and use its --mangoapp flag, which draws the same overlay " +
                "from the same MANGOHUD_CONFIG and composes reliably.");
        }

        if (HasArgument(options, InverseToneMappingFlags) && !HasArgument(options, HdrFlags))
        {
            warnings.Add(
                "Gamescope is set to expand SDR into HDR but not to output HDR. Add --hdr-enabled, " +
                "or the expanded image is tone mapped straight back down to SDR.");
        }

        if (!options.HasCommandPlaceholder && (options.Environment.Count > 0 || options.Wrapper.Count > 0))
        {
            warnings.Add(
                "There is no %command% placeholder, so Steam passes all of this to the game as " +
                "arguments instead of applying it. Add %command% at the end.");
        }

        if (IsOn(options, "PROTON_NO_ESYNC") && IsOn(options, "PROTON_NO_FSYNC"))
        {
            warnings.Add(
                "Both esync and fsync are disabled. Expect noticeably worse performance unless a " +
                "specific game needs it.");
        }

        return warnings;
    }

    private static bool IsSet(LaunchOptions options, string variable) =>
        options.FindEnvironment(variable)?.Value.Length > 0;

    private static bool IsOn(LaunchOptions options, string variable) =>
        options.FindEnvironment(variable) is { Value: var value } &&
        value.Length > 0 &&
        !string.Equals(value, "0", StringComparison.Ordinal);

    private static bool IsAnyOn(LaunchOptions options, string[] variables) =>
        variables.Any(variable => IsOn(options, variable));

    private static bool HasWrapper(LaunchOptions options, string command) =>
        options.Wrapper.Any(token =>
            string.Equals(Path.GetFileName(token), command, StringComparison.Ordinal));

    private static bool HasArgument(LaunchOptions options, params string[] spellings) =>
        options.Wrapper.Any(token => spellings.Contains(token, StringComparer.Ordinal));
}
