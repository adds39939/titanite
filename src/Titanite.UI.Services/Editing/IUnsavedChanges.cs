namespace Titanite.UI.Services.Editing;

public interface IUnsavedChanges
{
    bool Any { get; }

    IDisposable Track(Func<bool> hasChanges);
}
