namespace Titanite.Core.Launch;

public sealed partial record LaunchOptions
{
    private readonly record struct FlagLocation(int Index, int End, string? Inline);

    public bool HasCommand(CommandDefinition command) => IndexOfCommand(command.Command) >= 0;

    public LaunchOptions WithCommand(CommandDefinition command, bool present)
    {
        var index = IndexOfCommand(command.Command);

        if (present == index >= 0)
        {
            return this;
        }

        var wasEmpty = IsEmpty;
        var wrapper = Wrapper.ToList();

        if (present)
        {
            Insert(command, wrapper);
        }
        else
        {
            wrapper.RemoveRange(index, EndOfCommand(command, index, wrapper) - index);
        }

        return this with { Wrapper = wrapper, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

    public bool HasFlag(CommandDefinition command, CommandFlagDefinition flag) =>
        Locate(command, flag, Wrapper) is not null;

    public string? FindFlag(CommandDefinition command, CommandFlagDefinition flag)
    {
        if (Locate(command, flag, Wrapper) is not { } found)
        {
            return null;
        }

        if (found.Inline is { } inline)
        {
            return inline;
        }

        return flag.TakesValue && found.Index + 1 < found.End ? Wrapper[found.Index + 1] : null;
    }

    public LaunchOptions WithSwitch(CommandDefinition command, CommandFlagDefinition flag, bool present)
    {
        if (!present)
        {
            return Without(command, flag);
        }

        if (HasFlag(command, flag))
        {
            return this;
        }

        var (options, wrapper, index) = Ensure(command);

        wrapper.Insert(EndOfArguments(command, index, wrapper), flag.Flag);

        return options with { Wrapper = wrapper };
    }

    public LaunchOptions WithFlag(CommandDefinition command, CommandFlagDefinition flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Without(command, flag);
        }

        var trimmed = value.Trim();
        var (options, wrapper, index) = Ensure(command);

        if (Locate(command, flag, wrapper) is not { } found)
        {
            wrapper.InsertRange(EndOfArguments(command, index, wrapper), [flag.Flag, trimmed]);
        }
        else if (found.Inline is not null)
        {
            wrapper[found.Index] = $"{wrapper[found.Index].Split('=')[0]}={trimmed}";
        }
        else if (found.Index + 1 < found.End)
        {
            wrapper[found.Index + 1] = trimmed;
        }
        else
        {
            wrapper.Insert(found.Index + 1, trimmed);
        }

        return options with { Wrapper = wrapper };
    }

    private LaunchOptions Without(CommandDefinition command, CommandFlagDefinition flag)
    {
        if (Locate(command, flag, Wrapper) is not { } found)
        {
            return this;
        }

        var wrapper = Wrapper.ToList();
        var hasValueToken = found.Inline is null && flag.TakesValue && found.Index + 1 < found.End;

        wrapper.RemoveRange(found.Index, hasValueToken ? 2 : 1);

        return this with { Wrapper = wrapper };
    }

    private (LaunchOptions Options, List<string> Wrapper, int Index) Ensure(CommandDefinition command)
    {
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(command.Command, wrapper);

        if (index >= 0)
        {
            return (this, wrapper, index);
        }

        Insert(command, wrapper);

        return (this with { HasCommandPlaceholder = HasCommandPlaceholder || IsEmpty }, wrapper, 0);
    }

    private static void Insert(CommandDefinition command, List<string> wrapper) =>
        wrapper.InsertRange(
            0,
            command.Terminator is { } terminator ? [command.Command, terminator] : [command.Command]);

    private static FlagLocation? Locate(
        CommandDefinition command,
        CommandFlagDefinition flag,
        IReadOnlyList<string> wrapper)
    {
        var index = IndexOfCommand(command.Command, wrapper);

        if (index < 0)
        {
            return null;
        }

        var end = EndOfArguments(command, index, wrapper);

        for (var i = index + 1; i < end; i++)
        {
            if (FlagAt(command, wrapper[i]) is { } found &&
                string.Equals(found.Flag.Flag, flag.Flag, StringComparison.Ordinal))
            {
                return new FlagLocation(i, end, found.Inline);
            }
        }

        return null;
    }

    private static (CommandFlagDefinition Flag, string? Inline)? FlagAt(CommandDefinition command, string token)
    {
        foreach (var flag in command.AllFlags)
        {
            if (flag.Matches(token))
            {
                return (flag, null);
            }

            if (!flag.TakesValue)
            {
                continue;
            }

            var separator = token.IndexOf('=');

            if (separator > 0 && flag.Matches(token[..separator]))
            {
                return (flag, token[(separator + 1)..]);
            }
        }

        return null;
    }

    private static int EndOfArguments(CommandDefinition command, int index, IReadOnlyList<string> wrapper)
    {
        var start = index + 1;

        if (command.Terminator is { } terminator)
        {
            for (var i = start; i < wrapper.Count; i++)
            {
                if (string.Equals(wrapper[i], terminator, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        var end = start;

        while (end < wrapper.Count && FlagAt(command, wrapper[end]) is { } found)
        {
            end += found.Flag.TakesValue && found.Inline is null && end + 1 < wrapper.Count ? 2 : 1;
        }

        return end;
    }

    private static int EndOfCommand(CommandDefinition command, int index, IReadOnlyList<string> wrapper)
    {
        var end = EndOfArguments(command, index, wrapper);

        return command.Terminator is { } terminator &&
               end < wrapper.Count &&
               string.Equals(wrapper[end], terminator, StringComparison.Ordinal)
            ? end + 1
            : end;
    }
}
