using Plantech.Bim.Custom.Services;
using System;
using System.Windows;

namespace Plantech.Bim.Custom.Host;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!FilteredEvaluationService.IsTeklaConnected())
        {
            MessageBox.Show(
                "Tekla Structures is not connected. Start Tekla and open a model, then run this tool again.",
                "Plantech.Bim.Custom.Host",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose,
        };

        var window = new HostWindow();
        app.Run(window);
    }
}
