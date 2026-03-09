using Newtonsoft.Json;
using System;
using System.IO;

namespace Plantech.Bim.Custom.Configuration;

internal sealed class CustomAttributeConfigLoader
{
    private static readonly object SyncRoot = new();

    private readonly string _configFileName;
    private string _cachedPath = string.Empty;
    private DateTime _cachedWriteTimeUtc = DateTime.MinValue;
    private CustomAttributeConfig? _cachedConfig;

    public CustomAttributeConfigLoader(string configFileName)
    {
        _configFileName = configFileName ?? throw new ArgumentNullException(nameof(configFileName));
    }

    public CustomAttributeConfig? Load(string? modelPath)
    {
        var resolvedPath = ResolveConfigPath(modelPath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        var configPath = resolvedPath!;
        var writeTimeUtc = File.GetLastWriteTimeUtc(configPath);

        lock (SyncRoot)
        {
            if (string.Equals(_cachedPath, configPath, StringComparison.OrdinalIgnoreCase)
                && _cachedWriteTimeUtc == writeTimeUtc)
            {
                return _cachedConfig;
            }

            var json = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _cachedPath = configPath;
                _cachedWriteTimeUtc = writeTimeUtc;
                _cachedConfig = null;
                return null;
            }

            _cachedConfig = JsonConvert.DeserializeObject<CustomAttributeConfig>(json);
            _cachedPath = configPath;
            _cachedWriteTimeUtc = writeTimeUtc;
            return _cachedConfig;
        }
    }

    private string? ResolveConfigPath(string? modelPath)
    {
        foreach (var candidate in CustomConfigPaths.EnumerateCandidatePaths(_configFileName, modelPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
