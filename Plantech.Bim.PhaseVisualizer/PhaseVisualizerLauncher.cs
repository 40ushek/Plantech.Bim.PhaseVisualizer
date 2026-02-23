using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Plantech.Bim.PhaseVisualizer.UI;
using System;
using System.Windows;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer;

public static class PhaseVisualizerLauncher
{
    public static bool IsTeklaConnected()
    {
        try
        {
            return LazyModelConnector.ModelInstance.GetConnectionStatus();
        }
        catch
        {
            return false;
        }
    }

    public static void Open(SynchronizationContext? teklaContext = null)
    {
        var controller = new PhaseVisualizerController();
        var logDirectory = controller.ResolveEffectiveConfigDirectory(teklaContext);
        var log = PhaseVisualizerLogConfigurator.Configure(typeof(PhaseVisualizerLauncher), logDirectory);
        Application? app = null;
        var ownsApplication = false;

        if (Application.Current == null)
        {
            app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
            };
            ownsApplication = true;
        }

        try
        {
            var vm = new PhaseVisualizerViewModel(controller, teklaContext, log);
            var window = new PhaseVisualizerWindow(vm)
            {
                Topmost = true,
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PhaseVisualizer UI failed to open.");
        }
        finally
        {
            if (ownsApplication)
            {
                app?.Shutdown();
            }
        }
    }
}

