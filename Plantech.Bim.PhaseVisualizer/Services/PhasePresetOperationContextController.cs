using System;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetOperationContextController
{
    public bool TryCreate(
        string? presetName,
        string? stateFilePath,
        out PhasePresetOperationContext context)
    {
        context = PhasePresetOperationContext.Empty;
        if (presetName == null || stateFilePath == null)
        {
            return false;
        }

        var normalizedPresetName = presetName.Trim();
        if (normalizedPresetName.Length == 0
            || string.IsNullOrWhiteSpace(stateFilePath))
        {
            return false;
        }

        context = new PhasePresetOperationContext(
            normalizedPresetName,
            stateFilePath);
        return true;
    }
}

internal readonly struct PhasePresetOperationContext
{
    public static readonly PhasePresetOperationContext Empty = new(string.Empty, string.Empty);

    public PhasePresetOperationContext(string presetName, string stateFilePath)
    {
        PresetName = presetName ?? string.Empty;
        StateFilePath = stateFilePath ?? string.Empty;
    }

    public string PresetName { get; }

    public string StateFilePath { get; }
}
