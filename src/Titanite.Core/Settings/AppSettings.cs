namespace Titanite.Core.Settings;

public sealed record AppSettings
{
    public LibraryViewMode LibraryView { get; init; }

    public LibrarySortOrder LibrarySort { get; init; }

    public bool ShowVariableDescriptions { get; init; }

    public bool ShowNativeGames { get; init; }

    public bool ShowTools { get; init; }

    public int InterfaceScale { get; init; } = InterfaceScales.Default;

    public AppSettings Sanitised() => this with
    {
        LibraryView = Enum.IsDefined(LibraryView) ? LibraryView : default,
        LibrarySort = Enum.IsDefined(LibrarySort) ? LibrarySort : default,
        InterfaceScale = InterfaceScales.IsSupported(InterfaceScale) 
            ? InterfaceScale 
            : InterfaceScales.Default
    };
}
