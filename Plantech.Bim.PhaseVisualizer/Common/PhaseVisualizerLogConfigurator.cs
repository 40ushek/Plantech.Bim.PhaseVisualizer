using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Common;

internal static class PhaseVisualizerLogConfigurator
{
    private const string LogFileName = "phase-visualizer.log";
    private static readonly object SyncRoot = new();
    private static string _configuredDirectory = string.Empty;

    public static ILogger Configure(Type contextType, string? preferredDirectory)
    {
        EnsureConfigured(preferredDirectory);
        return Log.Logger.ForContext(contextType);
    }

    public static string ResolveLogPath(string? preferredDirectory)
    {
        var targetDirectory = ResolveTargetDirectory(preferredDirectory);
        return Path.Combine(targetDirectory, LogFileName);
    }

    private static void EnsureConfigured(string? preferredDirectory)
    {
        var targetDirectory = ResolveTargetDirectory(preferredDirectory);
        lock (SyncRoot)
        {
            if (string.Equals(_configuredDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Directory.CreateDirectory(targetDirectory);
            var logPath = Path.Combine(targetDirectory, LogFileName);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Infinite,
                    shared: true)
                .CreateLogger();

            _configuredDirectory = targetDirectory;
            Log.Logger.Information("PhaseVisualizer logging initialized. Path={LogPath}", logPath);
        }
    }

    private static string ResolveTargetDirectory(string? preferredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(preferredDirectory))
        {
            return preferredDirectory!;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Plantech",
            "PhaseVisualizer");
    }
}
