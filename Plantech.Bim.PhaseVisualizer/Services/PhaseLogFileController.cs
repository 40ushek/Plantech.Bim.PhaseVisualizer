using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseLogFileController
{
    private readonly PhaseVisualizerController _controller;
    private readonly SynchronizationContext? _teklaContext;
    private readonly ILogger _log;

    public PhaseLogFileController(
        PhaseVisualizerController controller,
        SynchronizationContext? teklaContext,
        ILogger log)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _teklaContext = teklaContext;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public PhaseLogOpenResult Open()
    {
        var logDirectory = _controller.ResolveEffectiveConfigDirectory(_teklaContext, _log);
        var logFilePath = PhaseVisualizerLogConfigurator.ResolveLogPath(logDirectory);
        try
        {
            if (!File.Exists(logFilePath))
            {
                return new PhaseLogOpenResult(false, $"Log file not found: {logFilePath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = logFilePath,
                UseShellExecute = true,
            });

            return new PhaseLogOpenResult(true, $"Opened log: {logFilePath}");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PhaseVisualizer failed to open log file. Path={LogPath}", logFilePath);
            return new PhaseLogOpenResult(false, $"Failed to open log: {logFilePath}");
        }
    }
}

internal sealed class PhaseLogOpenResult
{
    public PhaseLogOpenResult(bool isSuccess, string statusText)
    {
        IsSuccess = isSuccess;
        StatusText = statusText ?? string.Empty;
    }

    public bool IsSuccess { get; }

    public string StatusText { get; }
}
