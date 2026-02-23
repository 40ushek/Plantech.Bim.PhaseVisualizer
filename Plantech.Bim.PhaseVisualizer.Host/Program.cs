using Plantech.Bim.PhaseVisualizer;
using System;
using System.Windows;

namespace Plantech.Bim.PhaseVisualizer.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!PhaseVisualizerLauncher.IsTeklaConnected())
        {
            MessageBox.Show(
                "Tekla Structures is not connected. Start Tekla and open a model, then run this tool again.",
                "Phase Visualizer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        try
        {
            PhaseVisualizerLauncher.Open();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open Phase Visualizer:{Environment.NewLine}{ex.Message}",
                "Phase Visualizer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            app.Shutdown();
        }
    }
}

