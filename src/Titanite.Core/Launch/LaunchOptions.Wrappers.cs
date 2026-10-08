namespace Titanite.Core.Launch;

public sealed partial record LaunchOptions
{
    public bool HasWrapperCommand(string command) => IndexOfCommand(command) >= 0;

    public LaunchOptions WithWrapperCommand(string command, bool present)
    {
        var index = IndexOfCommand(command);

        if (present == index >= 0)
        {
            return this;
        }

        var wasEmpty = IsEmpty;
        var wrapper = Wrapper.ToList();

        if (present)
        {
            wrapper.Insert(0, command);
        }
        else
        {
            wrapper.RemoveAt(index);
        }

        return this with { Wrapper = wrapper, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

    private int IndexOfCommand(string command) => IndexOfCommand(command, Wrapper);

    private static int IndexOfCommand(string command, IReadOnlyList<string> wrapper)
    {
        for (var i = 0; i < wrapper.Count; i++)
        {
            if (string.Equals(Path.GetFileName(wrapper[i]), Path.GetFileName(command), StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
