using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseLocalUserStoragePathResolver
{
    private readonly string _localAppDataRoot;

    public PhaseLocalUserStoragePathResolver()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    internal PhaseLocalUserStoragePathResolver(string localAppDataRoot)
    {
        _localAppDataRoot = localAppDataRoot ?? string.Empty;
    }

    public PhaseLocalUserStoragePaths ResolveBase(string? modelPath)
    {
        var normalizedModelPath = NormalizeModelPath(modelPath);
        var modelKey = BuildModelKey(normalizedModelPath);
        var baseDirectory = Path.Combine(
            _localAppDataRoot,
            "Plantech",
            "PhaseVisualizer",
            modelKey);

        return new PhaseLocalUserStoragePaths(
            modelKey,
            baseDirectory,
            Path.Combine(baseDirectory, "session.json"),
            Path.Combine(normalizedModelPath, "attributes", "phase-visualizer.state.json"));
    }

    public string ResolveStateFilePath(PhaseLocalUserStoragePaths basePaths, string? profileKey)
    {
        if (basePaths == null)
        {
            throw new ArgumentNullException(nameof(basePaths));
        }

        if (string.IsNullOrWhiteSpace(basePaths.BaseDirectory))
        {
            return string.Empty;
        }

        return Path.Combine(
            basePaths.BaseDirectory,
            $"state.{SanitizeFileSegment(profileKey)}.json");
    }

    private static string NormalizeModelPath(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(modelPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return modelPath ?? string.Empty;
        }
    }

    private static string BuildModelKey(string normalizedModelPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedModelPath))
        {
            return "no-model";
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(normalizedModelPath.ToUpperInvariant());
        var hashBytes = sha256.ComputeHash(bytes);
        return string.Concat(hashBytes.Take(12).Select(b => b.ToString("x2")));
    }

    private static string SanitizeFileSegment(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "default"
            : value!.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            sanitized.Append(invalid.Contains(character) ? '_' : character);
        }

        return sanitized.Length == 0 ? "default" : sanitized.ToString();
    }
}

internal sealed class PhaseLocalUserStoragePaths
{
    public PhaseLocalUserStoragePaths(
        string modelKey,
        string baseDirectory,
        string sessionFilePath,
        string legacyStateFilePath)
    {
        ModelKey = modelKey ?? string.Empty;
        BaseDirectory = baseDirectory ?? string.Empty;
        SessionFilePath = sessionFilePath ?? string.Empty;
        LegacyStateFilePath = legacyStateFilePath ?? string.Empty;
    }

    public string ModelKey { get; }

    public string BaseDirectory { get; }

    public string SessionFilePath { get; }

    public string LegacyStateFilePath { get; }
}
