namespace Titanite.Catalog;

internal sealed class SettingDefinitionFile
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public int Order { get; set; }

    public List<SettingEntry> Settings { get; set; } = [];

    public List<SettingGroupBlock> Groups { get; set; } = [];

    public CommandBlock? Command { get; set; }

    internal sealed class SettingGroupBlock
    {
        public string? Name { get; set; }

        public List<SettingEntry> Settings { get; set; } = [];
    }

    internal sealed class SettingEntry
    {
        public string? Variable { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        public string? Kind { get; set; }

        public string? On { get; set; }

        public List<string> Choices { get; set; } = [];

        public string? Placeholder { get; set; }

        public List<string> ProtonBuilds { get; set; } = [];

        public bool RestrictToProtonBuild { get; set; }

        public bool HideUnlessSet { get; set; }

        public bool AllowEmpty { get; set; }

        public CompoundBlock? Compound { get; set; }
    }

    internal sealed class CompoundBlock
    {
        public string? Separator { get; set; }

        public string? Assignment { get; set; }

        public List<OptionGroup> Groups { get; set; } = [];
    }

    internal sealed class OptionGroup
    {
        public string? Name { get; set; }

        public List<OptionEntry> Options { get; set; } = [];
    }

    internal sealed class OptionEntry
    {
        public string? Key { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        public string? Kind { get; set; }

        public List<string> Choices { get; set; } = [];

        public string? Placeholder { get; set; }
    }

    internal sealed class CommandBlock
    {
        public string? Name { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        public string? Terminator { get; set; }

        public List<FlagGroup> Groups { get; set; } = [];
    }

    internal sealed class FlagGroup
    {
        public string? Name { get; set; }

        public List<FlagEntry> Flags { get; set; } = [];
    }

    internal sealed class FlagEntry
    {
        public string? Flag { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        public string? Kind { get; set; }

        public List<string> Choices { get; set; } = [];

        public List<string> Aliases { get; set; } = [];

        public string? Placeholder { get; set; }
    }
}
