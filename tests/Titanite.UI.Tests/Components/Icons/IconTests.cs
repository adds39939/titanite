using Bunit;
using Titanite.UI.Components.Icons;

namespace Titanite.UI.Tests.Components.Icons;

public sealed class IconTests : BunitContext
{
    public static TheoryData<Type> Icons =>
        [typeof(ChevronIcon), typeof(FilterIcon), typeof(GitHubIcon), typeof(GridIcon), typeof(ListIcon)];

    [Theory]
    [MemberData(nameof(Icons))]
    public void PaintsInTheColourOfWhateverHoldsIt(Type icon)
    {
        var svg = RenderIcon(icon);

        Assert.True(
            svg.GetAttribute("stroke") == "currentColor" || svg.GetAttribute("fill") == "currentColor",
            $"{icon.Name} paints in neither stroke nor fill of currentColor.");
    }

    [Theory]
    [MemberData(nameof(Icons))]
    public void NamesNothingAndTakesNoFocus(Type icon)
    {
        var svg = RenderIcon(icon);

        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Equal("false", svg.GetAttribute("focusable"));
    }

    [Theory]
    [MemberData(nameof(Icons))]
    public void CarriesItsOwnSize(Type icon)
    {
        var svg = RenderIcon(icon);

        Assert.True(svg.HasAttribute("width"));
        Assert.True(svg.HasAttribute("height"));
    }

    private AngleSharp.Dom.IElement RenderIcon(Type icon) =>
        Render(builder =>
        {
            builder.OpenComponent(0, icon);
            builder.CloseComponent();
        }).Find("svg");
}
