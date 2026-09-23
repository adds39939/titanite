namespace Titanite.Core.Settings;

public static class InterfaceScales
{
    public const int Default = 100;

    public static IReadOnlyList<int> Supported { get; } = [75, 80, 90, 100, 110, 125, 150, 175, 200];

    public static bool IsSupported(int percent) => Supported.Contains(percent);
}
