using Plantech.Bim.PhaseVisualizer.UI;
using Xunit;

namespace Plantech.Bim.PhaseVisualizer.Tests;

public sealed class PhaseVisualizerViewModelBehaviorTests
{
    [Fact]
    public void ResolveRequestedStateNameForLoad_ReturnsNullWithoutActiveProfile()
    {
        var selectedStateName = PhaseVisualizerViewModel.ResolveRequestedStateNameForLoad(
            selectedProfileKey: "",
            requestedStateName: "default");

        Assert.Null(selectedStateName);
    }

    [Fact]
    public void ResolveRequestedStateNameForLoad_ReturnsRequestedStateForActiveProfile()
    {
        var selectedStateName = PhaseVisualizerViewModel.ResolveRequestedStateNameForLoad(
            selectedProfileKey: "seva",
            requestedStateName: "review");

        Assert.Equal("review", selectedStateName);
    }
}
