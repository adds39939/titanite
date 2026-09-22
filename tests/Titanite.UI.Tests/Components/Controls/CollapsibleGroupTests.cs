using Bunit;
using Microsoft.AspNetCore.Components;
using Titanite.UI.Components.Controls;

namespace Titanite.UI.Tests.Components.Controls;

public sealed class CollapsibleGroupTests : BunitContext
{
    [Fact]
    public void ShowsTheCountBesideTheHeading()
    {
        var group = Render<CollapsibleGroup>(parameters => parameters
            .Add(component => component.Name, "Graphics")
            .Add(component => component.SetCount, 2)
            .AddChildContent("<p>body</p>"));

        Assert.Equal("Graphics", group.Find(".group-name").TextContent);
        Assert.Equal("2", group.Find(".group-count").TextContent);
    }

    [Fact]
    public void LeavesTheCountOutWhenNothingIsSet()
    {
        var group = Render<CollapsibleGroup>(parameters => parameters
            .Add(component => component.Name, "Graphics")
            .AddChildContent("<p>body</p>"));

        Assert.Empty(group.FindAll(".group-count"));
    }

    [Fact]
    public void RendersContentAloneWhenItHasNoHeading()
    {
        var group = Render<CollapsibleGroup>(parameters => parameters
            .AddChildContent("<p>body</p>"));

        Assert.Empty(group.FindAll("details"));
        Assert.Equal("body", group.Find("p").TextContent);
    }

    [Fact]
    public void OpensEveryRenderSoBlazorNeverClosesOneSomebodyOpened()
    {
        var group = Render<CollapsibleGroup>(parameters => parameters
            .Add(component => component.Name, "Graphics")
            .AddChildContent("<p>body</p>"));

        Assert.True(group.Find("details").HasAttribute("open"));

        group.Render(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["SetCount"] = 1 }));

        Assert.True(group.Find("details").HasAttribute("open"));
    }
}
