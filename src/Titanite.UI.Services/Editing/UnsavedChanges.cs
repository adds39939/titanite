namespace Titanite.UI.Services.Editing;

internal sealed class UnsavedChanges : IUnsavedChanges
{
    private readonly Lock _gate = new();

    private readonly List<Func<bool>> _sources = [];

    public bool Any
    {
        get
        {
            Func<bool>[] sources;

            lock (_gate)
            {
                sources = [.. _sources];
            }

            return sources.Any(hasChanges => hasChanges());
        }
    }

    public IDisposable Track(Func<bool> hasChanges)
    {
        lock (_gate)
        {
            _sources.Add(hasChanges);
        }

        return new Tracking(this, hasChanges);
    }

    private void Forget(Func<bool> hasChanges)
    {
        lock (_gate)
        {
            _sources.Remove(hasChanges);
        }
    }

    private sealed class Tracking(UnsavedChanges owner, Func<bool> hasChanges) : IDisposable
    {
        public void Dispose() => owner.Forget(hasChanges);
    }
}
