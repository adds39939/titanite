using Bunit;
using Microsoft.AspNetCore.Components;
using Titanite.UI.Components.Controls;

namespace Titanite.UI.Tests.Components.Controls;

public sealed class SingleSelectDropdownTests : BunitContext
{
    [Fact]
    public void KeepsTheMenuShutUntilItIsAskedFor()
    {
        var dropdown = RenderDropdown();

        Assert.Empty(dropdown.FindAll(".menu"));
        Assert.Equal("false", dropdown.Find(".dropdown > button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void OpensOnTheTrigger()
    {
        var dropdown = RenderDropdown();

        dropdown.Find(".dropdown > button").Click();

        Assert.Single(dropdown.FindAll(".menu"));
        Assert.Equal("true", dropdown.Find(".dropdown > button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ShutsAgainOnceSomethingIsChosen()
    {
        var chosen = 0;
        var dropdown = RenderDropdown(() => chosen++);

        dropdown.Find(".dropdown > button").Click();
        dropdown.Find(".item").Click();

        Assert.Equal(1, chosen);
        Assert.Empty(dropdown.FindAll(".menu"));
    }

    [Fact]
    public void ShutsWhenTheClickLandsAway()
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
    public void SaysNothingAboutSelectionWhenNoneWasGiven()
    {
        var dropdown = RenderDropdown();

        dropdown.Find(".dropdown > button").Click();

        var item = dropdown.Find(".item");

        Assert.Equal("menuitem", item.GetAttribute("role"));
        Assert.False(item.HasAttribute("aria-checked"));
    }

    [Fact]
    public void MarksTheOneCurrentlyChosen()
    {
        var dropdown = Render<SingleSelectDropdown>(parameters => parameters
            .Add(component => component.Label, "Sort by")
            .AddChildContent<SingleSelectDropdownItem>(item => item
                .Add(component => component.IsSelected, true)
                .AddChildContent("Name")));

        dropdown.Find(".dropdown > button").Click();

        var item = dropdown.Find(".item");

        Assert.Equal("menuitemradio", item.GetAttribute("role"));
        Assert.Equal("true", item.GetAttribute("aria-checked"));
        Assert.Contains("is-selected", item.ClassList);
    }

    private IRenderedComponent<SingleSelectDropdown> RenderDropdown(Action? onSelect = null) =>
        Render<SingleSelectDropdown>(parameters => parameters
            .Add(component => component.Label, "Open")
            .AddChildContent<SingleSelectDropdownItem>(item => item
                .Add(component => component.OnSelect, EventCallback.Factory.Create(this, onSelect ?? (() => { })))
                .AddChildContent("Game install")));
}
