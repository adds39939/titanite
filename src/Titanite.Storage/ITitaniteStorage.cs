namespace Titanite.Storage;

public interface ITitaniteStorage
{
    string Root { get; }

    string ProfileFile { get; }

    string PresetsFile { get; }

    string SettingsFile { get; }
}
