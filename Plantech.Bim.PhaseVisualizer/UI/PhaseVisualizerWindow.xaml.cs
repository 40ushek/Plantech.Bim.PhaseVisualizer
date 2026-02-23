using System;
using System.ComponentModel;
using System.Windows;

namespace Plantech.Bim.PhaseVisualizer.UI;

public partial class PhaseVisualizerWindow : Window
{
    internal PhaseVisualizerWindow(PhaseVisualizerViewModel viewModel)
    {
        if (viewModel == null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        InitializeComponent();
        MainView.Initialize(viewModel);
        MainView.RequestClose += (_, _) => Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        MainView.TrySaveState();
        base.OnClosing(e);
    }
}
