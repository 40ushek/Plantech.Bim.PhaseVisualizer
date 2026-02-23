using System.Collections.Generic;
using Tekla.Structures.Plugins;

namespace Plantech.Bim.PhaseVisualizer.Plugins;

[Plugin(PluginName)]
[PluginUserInterface("Plantech.Bim.PhaseVisualizer.Plugins.PhaseVisualizerWindow")]
[InputObjectDependency(InputObjectDependency.NOT_DEPENDENT)]
internal class PhaseVisualizerPlugin : PluginBase
{
    public const string PluginName = "ptPhaseVisualizer";
    private static readonly List<InputDefinition> InputDefinitions = new();

    public override List<InputDefinition> DefineInput()
    {
        return InputDefinitions;
    }

    public override bool Run(List<InputDefinition> Input) => true;
}
