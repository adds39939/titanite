using Bunit;
using Microsoft.AspNetCore.Components;
using Titanite.UI.Components.Controls;
using Titanite.UI.Components.Icons;

namespace Titanite.UI.Tests.Components.Controls;

public sealed class MultiSelectDropdownTests : BunitContext
{
    [Fact]
    public void KeepsTheMenuShutUntilItIsAskedFor()
    {
        var dropdown = RenderDropdown();

        Assert.Empty(dropdown.FindAll(".menu"));
        Assert.Equal("false", dropdown.Find(".dropdown > button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void StaysOpenWhileOptionsAreTicked()
    {
        var ticked = new List<bool>();
        var dropdown = RenderDropdown(isOn => ticked.Add(isOn));

        dropdown.Find(".dropdown > button").Click();
        dropdown.Find(".option input").Change(true);

        Assert.Equal([true], ticked);
        Assert.Single(dropdown.FindAll(".menu"));
    }

    [Fact]
    public void LightsItsTriggerWhileTheMenuIsOpen()
    {
        var dropdown = RenderDropdown();

        Assert.DoesNotContain("is-open", dropdown.Find(".dropdown").ClassList);

        dropdown.Find(".dropdown > button").Click();

        Assert.Contains("is-open", dropdown.Find(".dropdown").ClassList);
    }

    [Fact]
    public void ShutsOnlyWhenTheClickLandsAway()
    {
        var dropdown = RenderDropdown();

        dropdown.Find(".dropdown > button").Click();
        dropdown.Find(".scrim").Click();

        Assert.Empty(dropdown.FindAll(".menu"));
    }

    [Fact]
    public void ShutsOnEscape()
    {
        var dropdown = RenderDropdown();

        dropdown.Find(".dropdown > button").Click();
        dropdown.Find(".dropdown").KeyDown("Escape");

        Assert.Empty(dropdown.FindAll(".menu"));
    }

    [Fact]
    public void PutsTheTickBesideTheNameItBelongsTo()
    {
        var dropdown = RenderDropdown(isChecked: true);

        dropdown.Find(".dropdown > button").Click();

        var option = dropdown.Find(".option");

        Assert.Equal("menuitemcheckbox", option.GetAttribute("role"));
        Assert.Equal("true", option.GetAttribute("aria-checked"));
        Assert.Equal("Show tools", option.QuerySelector(".option-label")!.TextContent);
        Assert.True(option.QuerySelector("input")!.HasAttribute("checked"));
    }

    [Fact]
    public void UntiesTheTickFromTheOptionWhenItIsOff()
    {
        var dropdown = RenderDropdown();

        dropdown.Find(".dropdown > button").Click();

        Assert.Equal("false", dropdown.Find(".option").GetAttribute("aria-checked"));
        Assert.False(dropdown.Find(".option input").HasAttribute("checked"));
    }

    [Fact]
    public void NamesItselfForTheReaderWhenAllItShowsIsAnIcon()
    {
        var dropdown = Render<MultiSelectDropdown>(parameters => parameters
            .Add(component => component.Label, "Filters")
            .Add<FilterIcon>(component => component.Icon)
            .AddChildContent<MultiSelectDropdownOption>(option => option.AddChildContent("Show tools")));

        var trigger = dropdown.Find(".dropdown > button");

        Assert.Equal("Filters", trigger.GetAttribute("aria-label"));
        Assert.Equal("Filters", trigger.GetAttribute("title"));
        Assert.Empty(dropdown.FindAll(".label"));
        Assert.Single(trigger.QuerySelectorAll("svg"));
    }

    [Fact]
    public void FallsBackToTheWrittenNameWhenThereIsNoIcon()
    {
        var dropdown = RenderDropdown();

        Assert.Equal("Filters", dropdown.Find(".label").TextContent);
        Assert.Single(dropdown.Find(".dropdown > button").QuerySelectorAll("svg"));
    }

    private IRenderedComponent<MultiSelectDropdown> RenderDropdown(
        Action<bool>? onChange = null,
        bool isChecked = false) =>
        Render<MultiSelectDropdown>(parameters => parameters
            .Add(component => component.Label, "Filters")
            .AddChildContent<MultiSelectDropdownOption>(option => option
                .Add(component => component.IsChecked, isChecked)
                .Add(
                    component => component.IsCheckedChanged,
                    EventCallback.Factory.Create(this, onChange ?? (_ => { })))
                .AddChildContent("Show tools")));
}
