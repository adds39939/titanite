namespace Titanite.Storage;

public sealed class TitaniteStorage : ITitaniteStorage
{
    public TitaniteStorage()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "titanite"))
    {
    }

    private TitaniteStorage(string root) => Root = root;

    public static TitaniteStorage At(string root) => new(root);

    public string Root { get; }

    public string ProfileFile => Path.Combine(Root, "profile.json");

    public string PresetsFile => Path.Combine(Root, "presets.json");

    public string SettingsFile => Path.Combine(Root, "settings.json");
}
