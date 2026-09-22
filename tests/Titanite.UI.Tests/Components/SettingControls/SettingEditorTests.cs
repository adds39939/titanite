using Bunit;
using Titanite.Core.Launch;
using Titanite.UI.Components.SettingControls;

namespace Titanite.UI.Tests.Components.SettingControls;

public sealed class SettingEditorTests : BunitContext
{
    [Fact]
    public void RendersAToggleAsASwitch()
    {
        var editor = Render(Definition(SettingKind.Toggle), "1");

        Assert.Equal("checkbox", editor.Find(".setting-control input").GetAttribute("type"));
        Assert.Equal("On", editor.Find(".switch span").TextContent);
    }

    [Fact]
    public void RendersAChoiceAsASelectThatCanBeUnset()
    {
        var editor = Render(Definition(SettingKind.Choice) with { Choices = ["vulkan", "gl"] }, "gl");

        string[] options = [.. editor.FindAll(".setting-control option").Select(option => option.TextContent)];

        Assert.Equal(["Not set", "vulkan", "gl"], options);
        Assert.Equal("gl", editor.Find(".setting-control select").GetAttribute("value"));
    }

    [Fact]
    public void KeepsAStoredChoiceItDoesNotRecognise()
    {
        var editor = Render(Definition(SettingKind.Choice) with { Choices = ["vulkan"] }, "d3d12");

        Assert.Contains("d3d12", editor.FindAll(".setting-control option").Select(option => option.TextContent));
    }

    [Fact]
    public void RendersANumberAsANumberField()
    {
        var editor = Render(Definition(SettingKind.Number), "60");

        Assert.Equal("number", editor.Find(".setting-control input").GetAttribute("type"));
    }

    [Fact]
    public void RendersTextAsATextField()
    {
        var editor = Render(Definition(SettingKind.Text), "anything");

        Assert.Equal("text", editor.Find(".setting-control input").GetAttribute("type"));
    }

    [Fact]
    public void HandsACompoundVariableToItsOwnEditor()
    {
        var definition = Definition(SettingKind.Text) with
        {
            Compound = new CompoundSchema(
                CompoundSchema.DefaultSeparator,
                CompoundSchema.DefaultAssignment,
                [new CompoundOptionGroup(null, [new CompoundOptionDefinition("fps", "Frame rate")])])
        };

        var editor = Render(definition, null);

        Assert.NotEmpty(editor.FindAll(".setting-compound"));
        Assert.Empty(editor.FindAll(".setting-control"));
    }

    [Fact]
    public void ClearingAFieldRemovesTheVariable()
    {
        string? saved = "60";

        var editor = base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, Definition(SettingKind.Number))
            .Add(component => component.Value, saved)
            .Add(component => component.ValueChanged, value => saved = value));

        editor.Find(".setting-control input").Change("   ");

        Assert.Null(saved);
    }

    [Fact]
    public void ClearingAFieldThatAllowsEmptyKeepsTheVariableSetEmpty()
    {
        string? saved = "/usr/lib/libfoo.so";

        var editor = base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, Definition(SettingKind.Text) with { AllowEmpty = true })
            .Add(component => component.Value, saved)
            .Add(component => component.ValueChanged, value => saved = value));

        editor.Find(".setting-control input").Change("   ");

        Assert.Equal(string.Empty, saved);
    }

    [Fact]
    public void OffersToSetEmptyWhereNothingIsSet()
    {
        string? saved = null;

        var editor = base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, Definition(SettingKind.Text) with { AllowEmpty = true })
            .Add(component => component.ValueChanged, value => saved = value));

        editor.Find(".setting-control button").Click();

        Assert.Equal(string.Empty, saved);
    }

    [Fact]
    public void RemovesAVariableSetEmptyWithItsOwnButton()
    {
        string? saved = string.Empty;

        var editor = base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, Definition(SettingKind.Text) with { AllowEmpty = true })
            .Add(component => component.Value, saved)
            .Add(component => component.ValueChanged, value => saved = value));

        Assert.Equal("Set, empty", editor.Find(".setting-control input").GetAttribute("placeholder"));

        editor.Find(".setting-control button").Click();

        Assert.Null(saved);
    }

    [Fact]
    public void SaysWhenTheBuildWillNotReadTheVariable()
    {
        var editor = base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, Definition(SettingKind.Toggle))
            .Add(component => component.IsIgnored, true)
            .Add(component => component.BuildName, "Proton 7.0"));

        Assert.Contains("is-ignored", editor.Find(".setting").ClassList);
        Assert.Contains("Proton 7.0", editor.Find(".setting-ignored").TextContent);
    }

    private static SettingDefinition Definition(SettingKind kind) =>
        new("PROTON_TEST", new SettingCategory("test", "Test", 0), "Test setting") { Kind = kind };

    private IRenderedComponent<SettingEditor> Render(SettingDefinition definition, string? value) =>
        base.Render<SettingEditor>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Value, value));
}
