namespace Titanite.Core.Settings;

public static class InterfaceScales
{
    public const int Default = 100;
    public const int Minimum = 75;
    public const int Maximum = 200;
    public const int Step = 25;

    public static IReadOnlyList<int> Supported { get; } =
        Enumerable.Range(0, (Maximum - Minimum) / Step + 1).Select(index => Minimum + index * Step).ToArray();

    public static bool IsSupported(int percent) => Supported.Contains(percent);
}
