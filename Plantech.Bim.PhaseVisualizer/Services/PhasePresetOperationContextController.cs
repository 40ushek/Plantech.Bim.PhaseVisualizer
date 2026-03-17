using System;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetOperationContextController
{
    public bool TryCreate(
        string? presetName,
        string? stateFilePath,
        string? configFingerprint,
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
            stateFilePath,
            configFingerprint);
        return true;
    }
}

internal readonly struct PhasePresetOperationContext
{
    public static readonly PhasePresetOperationContext Empty = new(string.Empty, string.Empty, string.Empty);

    public PhasePresetOperationContext(string presetName, string stateFilePath, string? configFingerprint)
    {
        PresetName = presetName ?? string.Empty;
        StateFilePath = stateFilePath ?? string.Empty;
        ConfigFingerprint = configFingerprint ?? string.Empty;
    }

    public string PresetName { get; }

    public string StateFilePath { get; }

    public string ConfigFingerprint { get; }
}
