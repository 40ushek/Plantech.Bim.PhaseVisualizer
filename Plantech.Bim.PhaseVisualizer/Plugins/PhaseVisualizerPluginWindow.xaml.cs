using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Plantech.Bim.PhaseVisualizer.UI;
using System;
using System.ComponentModel;
using System.Threading;
using Tekla.Structures.Dialog;

namespace Plantech.Bim.PhaseVisualizer.Plugins;

public partial class PhaseVisualizerWindow : PluginWindowBase
{
    private readonly UI.PhaseVisualizerViewModel _uiViewModel;

    public PhaseVisualizerWindow(PhaseVisualizerPluginViewModel pluginViewModel)
    {
        if (pluginViewModel == null)
        {
            throw new ArgumentNullException(nameof(pluginViewModel));
        }

        InitializeComponent();
        DataContext = pluginViewModel;
        _uiViewModel = CreateDefaultViewModel();
        MainView.Initialize(_uiViewModel);
        MainView.RequestClose += (_, _) => Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        MainView.TrySaveState();
        base.OnClosing(e);
    }

    private static UI.PhaseVisualizerViewModel CreateDefaultViewModel()
    {
        var controller = new PhaseVisualizerController();
        var logDirectory = controller.ResolveEffectiveConfigDirectory(SynchronizationContext.Current);
        var log = PhaseVisualizerLogConfigurator.Configure(typeof(PhaseVisualizerWindow), logDirectory);
        return new UI.PhaseVisualizerViewModel(controller, SynchronizationContext.Current, log);
    }
}
