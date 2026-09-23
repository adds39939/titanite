using Titanite.UI.Services.Editing;

namespace Titanite.UI.Services.Tests.Editing;

public class UnsavedChangesTests
{
    private readonly UnsavedChanges _changes = new();

    [Fact]
    public void HasNothingUnsavedWhenNothingIsTracked() =>
        Assert.False(_changes.Any);

    [Fact]
    public void HasSomethingUnsavedWhenAnyTrackedScreenDoes()
    {
        _changes.Track(() => false);
        _changes.Track(() => true);

        Assert.True(_changes.Any);
    }

    [Fact]
    public void AsksEachTimeRatherThanRemembering()
    {
        var dirty = false;

        _changes.Track(() => dirty);

        Assert.False(_changes.Any);

        dirty = true;

        Assert.True(_changes.Any);
    }

    [Fact]
    public void ForgetsAScreenThatHasClosed()
    {
        var tracking = _changes.Track(() => true);

        tracking.Dispose();

        Assert.False(_changes.Any);
    }
}
