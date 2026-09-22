using System.Text.RegularExpressions;

namespace Titanite.Core.Proton;

public static partial class ProtonToolName
{
    public static string Derive(string appName)
    {
        var name = appName.Trim();
        var version = VersionedName().Match(name);

        if (version.Success)
        {
            var major = version.Groups["major"].Value;
            var minor = version.Groups["minor"].Value;

            return minor == "0" ? $"proton_{major}" : $"proton_{major}{minor}";
        }

        return Separators().Replace(name.ToLowerInvariant(), "_").Trim('_');
    }

    [GeneratedRegex(@"^proton\s+(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionedName();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Separators();
}
