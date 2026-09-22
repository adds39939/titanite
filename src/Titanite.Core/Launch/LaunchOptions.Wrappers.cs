namespace Titanite.Core.Launch;

public sealed partial record LaunchOptions
{
    public const string TasksetCommand = "taskset";

    private static readonly string[] TasksetListFlags = ["-c", "--cpu-list"];

    public string? CpuAffinity
    {
        get
        {
            var index = IndexOfCommand(TasksetCommand);

            return index >= 0 && index + 2 < Wrapper.Count && TasksetListFlags.Contains(Wrapper[index + 1])
                ? Wrapper[index + 2]
                : null;
        }
    }

    public LaunchOptions WithCpuAffinity(string? mask)
    {
        var wasEmpty = IsEmpty;
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(TasksetCommand, wrapper);
        var hasList = index >= 0 && index + 2 < wrapper.Count && TasksetListFlags.Contains(wrapper[index + 1]);

        if (string.IsNullOrWhiteSpace(mask))
        {
            if (index >= 0)
            {
                wrapper.RemoveRange(index, hasList ? 3 : 1);
            }

            return this with { Wrapper = wrapper };
        }

        if (hasList)
        {
            wrapper[index + 2] = mask.Trim();
        }
        else
        {
            if (index >= 0)
            {
                wrapper.RemoveAt(index);
            }

            wrapper.AddRange([TasksetCommand, "-c", mask.Trim()]);
        }

        return this with { Wrapper = wrapper, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

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
