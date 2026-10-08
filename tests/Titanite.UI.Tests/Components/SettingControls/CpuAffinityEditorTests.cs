using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Cpu;
using Titanite.Core.Cpu;
using Titanite.UI.Components.SettingControls;

namespace Titanite.UI.Tests.Components.SettingControls;

public sealed class CpuAffinityEditorTests : BunitContext
{
    private readonly ICpuTopologyService _topology = A.Fake<ICpuTopologyService>();

    public CpuAffinityEditorTests()
    {
        A.CallTo(() => _topology.Get()).Returns(new CpuTopology());

        Services.AddSingleton(_topology);
    }

    [Fact]
    public void OffersWineTopologyAndTaskset()
    {
        var tokens = Render(CpuAffinityMethod.WineCpuTopology)
            .FindAll(".method-token")
            .Select(token => token.TextContent);

        Assert.Equal(["WINE_CPU_TOPOLOGY", "taskset -c"], tokens);
    }

    [Theory]
    [InlineData(CpuAffinityMethod.WineCpuTopology, "Wine topology")]
    [InlineData(CpuAffinityMethod.Taskset, "taskset")]
    public void MarksTheMethodInUse(CpuAffinityMethod method, string label)
    {
        var active = Render(method).Find(".method.is-active");

        Assert.Equal(label, active.QuerySelector(".method-label")!.TextContent);
        Assert.Equal("true", active.GetAttribute("aria-checked"));
    }

    [Fact]
    public void ReportsTheMethodPicked()
    {
        CpuAffinityMethod? picked = null;

        var editor = Render(CpuAffinityMethod.WineCpuTopology, method => picked = method);

        editor.FindAll(".method")[1].Click();

        Assert.Equal(CpuAffinityMethod.Taskset, picked);
    }

    [Fact]
    public void StaysQuietWhenTheMethodInUseIsPickedAgain()
    {
        CpuAffinityMethod? picked = null;

        var editor = Render(CpuAffinityMethod.WineCpuTopology, method => picked = method);

        editor.Find(".method.is-active").Click();

        Assert.Null(picked);
    }

    [Theory]
    [InlineData(CpuAffinityMethod.WineCpuTopology, "WINE_CPU_TOPOLOGY")]
    [InlineData(CpuAffinityMethod.Taskset, "taskset -c")]
    public void NamesWhereTheMaskGoes(CpuAffinityMethod method, string destination) =>
        Assert.Equal(destination, Render(method).Find(".group-note code").TextContent);

    private IRenderedComponent<CpuAffinityEditor> Render(
        CpuAffinityMethod method,
        Action<CpuAffinityMethod>? picked = null) =>
        base.Render<CpuAffinityEditor>(parameters => parameters
            .Add(editor => editor.Method, method)
            .Add(editor => editor.MethodChanged, picked ?? (_ => { })));
}
