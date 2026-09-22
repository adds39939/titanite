using Bunit;
using Titanite.UI.Components.Controls;

namespace Titanite.UI.Tests.Components.Controls;

public sealed class ButtonTests : BunitContext
{
    [Theory]
    [InlineData(ButtonVariant.Primary, "button-primary")]
    [InlineData(ButtonVariant.Destructive, "button-destructive")]
    [InlineData(ButtonVariant.Danger, "button-danger")]
    [InlineData(ButtonVariant.Quiet, "button-quiet")]
    [InlineData(ButtonVariant.Bare, "button-bare")]
    public void MapsEachVariantToItsClass(ButtonVariant variant, string expected)
    {
        var button = RenderButton(parameters => parameters.Add(component => component.Variant, variant));

        Assert.Contains(expected, button.Find("button").ClassList);
    }

    [Fact]
    public void LeavesADefaultButtonWithTheBaseClassAlone()
    {
        var button = RenderButton(_ => { });

        Assert.Equal(["button"], button.Find("button").ClassList);
    }

    [Fact]
    public void AsksForTheSmallSizeOnlyWhenItIsSmall()
    {
        Assert.Contains("button-small", RenderButton(parameters => parameters
            .Add(component => component.Size, ButtonSize.Small)).Find("button").ClassList);

        Assert.DoesNotContain("button-small", RenderButton(parameters => parameters
            .Add(component => component.Size, ButtonSize.Medium)).Find("button").ClassList);
    }

    [Fact]
    public void MarksAnUnavailableActionDisabled()
    {
        var button = RenderButton(parameters => parameters
            .Add(component => component.Variant, ButtonVariant.Danger)
            .Add(component => component.Disabled, true));

        Assert.True(button.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void CarriesWhateverElseTheCallerSets()
    {
        var button = RenderButton(parameters => parameters
            .AddUnmatched("aria-label", "Rescan"));

        Assert.Equal("Rescan", button.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public void RaisesItsCallbackWhenClicked()
    {
        var clicks = 0;

        var button = RenderButton(parameters => parameters
            .Add(component => component.OnClick, () => clicks++));

        button.Find("button").Click();

        Assert.Equal(1, clicks);
    }

    private IRenderedComponent<Button> RenderButton(
        Action<ComponentParameterCollectionBuilder<Button>> parameters) =>
        Render<Button>(builder =>
        {
            builder.AddChildContent("Rescan");
            parameters(builder);
        });
}
