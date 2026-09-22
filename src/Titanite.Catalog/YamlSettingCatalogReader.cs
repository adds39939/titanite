using Microsoft.Extensions.Logging;
using Titanite.Core.Launch;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace Titanite.Catalog;

public sealed class YamlSettingCatalogReader(string directory, ILogger<YamlSettingCatalogReader> logger)
{
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "settings");

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public SettingCatalog Read()
    {
        string[] paths;

        try
        {
            if (!Directory.Exists(directory))
            {
                logger.LogWarning("No setting definitions were found at {Directory}.", directory);

                return SettingCatalog.Empty;
            }

            paths = Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not list setting definitions in {Directory}.", directory);

            return SettingCatalog.Empty;
        }

        var categories = new List<SettingCategory>();
        var definitions = new List<SettingDefinition>();

        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            if (ReadFile(path) is not { } file || file.Id is not { Length: > 0 } id)
            {
                continue;
            }

            var category = new SettingCategory(id, file.Title ?? id, file.Order)
            {
                Command = Convert(file.Command, path)
            };

            categories.Add(category);

            definitions.AddRange(file.Settings
                .Select(entry => Convert(entry, category, null, path))
                .OfType<SettingDefinition>());

            definitions.AddRange(file.Groups.SelectMany(group => group.Settings
                .Select(entry => Convert(entry, category, group.Name is { Length: > 0 } name ? name : null, path))
                .OfType<SettingDefinition>()));
        }

        logger.LogInformation(
            "Read {SettingCount} settings in {CategoryCount} sections from {Directory}.",
            definitions.Count,
            categories.Count,
            directory);

        return new SettingCatalog(categories, definitions);
    }

    private SettingDefinitionFile? ReadFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var file = Deserializer.Deserialize<SettingDefinitionFile>(text);

            if (file is null || string.IsNullOrWhiteSpace(file.Id))
            {
                logger.LogWarning("Skipped {Path}; it names no section id.", path);

                return null;
            }

            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or YamlException)
        {
            logger.LogError(e, "Skipped {Path}; it could not be read.", path);

            return null;
        }
    }

    private SettingDefinition? Convert(
        SettingDefinitionFile.SettingEntry entry,
        SettingCategory category,
        string? group,
        string path)
    {
        if (entry.Variable is not { Length: > 0 } variable)
        {
            logger.LogWarning("Skipped a setting in {Path}; it names no variable.", path);

            return null;
        }

        return new SettingDefinition(variable, category, entry.Label ?? variable)
        {
            Description = entry.Description,
            Group = group,
            Kind = ParseKind(entry.Kind, variable, path),
            OnValue = entry.On is { Length: > 0 } on ? on : "1",
            Choices = entry.Choices,
            Placeholder = entry.Placeholder,
            ProtonBuilds = entry.ProtonBuilds,
            RestrictToProtonBuild = entry.RestrictToProtonBuild,
            HideUnlessSet = entry.HideUnlessSet,
            AllowEmpty = entry.AllowEmpty,
            Compound = Convert(entry.Compound, variable, path)
        };
    }

    private CompoundSchema? Convert(SettingDefinitionFile.CompoundBlock? block, string variable, string path)
    {
        if (block is null)
        {
            return null;
        }

        var groups = block.Groups
            .Select(group => new CompoundOptionGroup(
                group.Name,
                group.Options
                    .Select(option => Convert(option, variable, path))
                    .OfType<CompoundOptionDefinition>()
                    .ToList()))
            .Where(group => group.Options.Count > 0)
            .ToList();

        if (groups.Count == 0)
        {
            logger.LogWarning("{Variable} in {Path} declares a compound with no options.", variable, path);

            return null;
        }

        return new CompoundSchema(
            block.Separator is { Length: > 0 } separator ? separator : CompoundSchema.DefaultSeparator,
            block.Assignment is { Length: > 0 } assignment ? assignment : CompoundSchema.DefaultAssignment,
            groups);
    }

    private CompoundOptionDefinition? Convert(SettingDefinitionFile.OptionEntry option, string variable, string path)
    {
        if (option.Key is not { Length: > 0 } key)
        {
            logger.LogWarning("Skipped an option of {Variable} in {Path}; it names no key.", variable, path);

            return null;
        }

        return new CompoundOptionDefinition(key, option.Label ?? key)
        {
            Kind = option.Kind is { Length: > 0 }
                ? ParseKind(option.Kind, $"{variable}.{key}", path)
                : SettingKind.Toggle,
            Choices = option.Choices,
            Placeholder = option.Placeholder,
            Description = option.Description
        };
    }

    private CommandDefinition? Convert(SettingDefinitionFile.CommandBlock? block, string path)
    {
        if (block is null)
        {
            return null;
        }

        if (block.Name is not { Length: > 0 } name)
        {
            logger.LogWarning("Skipped the command in {Path}; it names no command to run.", path);

            return null;
        }

        return new CommandDefinition(name, block.Label ?? name)
        {
            Description = block.Description,
            Terminator = block.Terminator is { Length: > 0 } terminator ? terminator : null,
            Groups = block.Groups
                .Select(group => new CommandFlagGroup(
                    group.Name,
                    group.Flags
                        .Select(flag => Convert(flag, name, path))
                        .OfType<CommandFlagDefinition>()
                        .ToList()))
                .Where(group => group.Flags.Count > 0)
                .ToList()
        };
    }

    private CommandFlagDefinition? Convert(SettingDefinitionFile.FlagEntry entry, string command, string path)
    {
        if (entry.Flag is not { Length: > 0 } flag)
        {
            logger.LogWarning("Skipped a flag of {Command} in {Path}; it names no flag.", command, path);

            return null;
        }

        return new CommandFlagDefinition(flag, entry.Label ?? flag)
        {
            Kind = entry.Kind is { Length: > 0 }
                ? ParseKind(entry.Kind, $"{command} {flag}", path)
                : SettingKind.Toggle,
            Choices = entry.Choices,
            Aliases = entry.Aliases,
            Placeholder = entry.Placeholder,
            Description = entry.Description
        };
    }

    private SettingKind ParseKind(string? kind, string variable, string path)
    {
        if (kind is null or { Length: 0 })
        {
            return SettingKind.Text;
        }

        if (!char.IsAsciiDigit(kind[0]) && Enum.TryParse<SettingKind>(kind, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        logger.LogWarning(
            "{Variable} in {Path} asks for an unknown kind '{Kind}', so it is edited as text.",
            variable,
            path,
            kind);

        return SettingKind.Text;
    }
}
