using System.ComponentModel;

namespace Plantech.Bim.PhaseVisualizer.Plugins;

// Tekla instantiates this view-model for PluginWindowBase constructor injection.
public sealed class PhaseVisualizerPluginViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }
}
