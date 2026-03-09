using Newtonsoft.Json;
using System;
using System.IO;

namespace Plantech.Bim.Custom.Configuration;

internal sealed class CustomAttributeConfigLoader
{
    private static readonly object SyncRoot = new();
    private static readonly TimeSpan HotCacheWindow = TimeSpan.FromSeconds(2);

    private readonly string _configFileName;
    private string _cachedModelPathKey = string.Empty;
    private string _cachedPath = string.Empty;
    private DateTime _cachedWriteTimeUtc = DateTime.MinValue;
    private DateTime _cachedLastAccessUtc = DateTime.MinValue;
    private CustomAttributeConfig? _cachedConfig;

    public CustomAttributeConfigLoader(string configFileName)
    {
        _configFileName = configFileName ?? throw new ArgumentNullException(nameof(configFileName));
    }

    public CustomAttributeConfig? Load(string? modelPath)
    {
        return LoadSnapshot(modelPath).Config;
    }

    public ConfigSnapshot LoadSnapshot(string? modelPath)
    {
        var modelPathKey = modelPath?.Trim() ?? string.Empty;
        var nowUtc = DateTime.UtcNow;

        lock (SyncRoot)
        {
            if (string.Equals(_cachedModelPathKey, modelPathKey, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_cachedPath)
                && nowUtc - _cachedLastAccessUtc <= HotCacheWindow)
            {
                _cachedLastAccessUtc = nowUtc;
                return new ConfigSnapshot(_cachedPath, _cachedConfig);
            }
        }

        var resolvedPath = ResolveConfigPath(modelPath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            lock (SyncRoot)
            {
                _cachedModelPathKey = modelPathKey;
                _cachedPath = string.Empty;
                _cachedWriteTimeUtc = DateTime.MinValue;
                _cachedLastAccessUtc = nowUtc;
                _cachedConfig = null;
            }
            return new ConfigSnapshot(string.Empty, null);
        }

        var configPath = resolvedPath!;
        var writeTimeUtc = File.GetLastWriteTimeUtc(configPath);

        lock (SyncRoot)
        {
            if (string.Equals(_cachedModelPathKey, modelPathKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_cachedPath, configPath, StringComparison.OrdinalIgnoreCase)
                && _cachedWriteTimeUtc == writeTimeUtc)
            {
                _cachedLastAccessUtc = nowUtc;
                return new ConfigSnapshot(_cachedPath, _cachedConfig);
            }

            var json = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _cachedModelPathKey = modelPathKey;
                _cachedPath = configPath;
                _cachedWriteTimeUtc = writeTimeUtc;
                _cachedLastAccessUtc = nowUtc;
                _cachedConfig = null;
                return new ConfigSnapshot(_cachedPath, null);
            }

            _cachedConfig = JsonConvert.DeserializeObject<CustomAttributeConfig>(json);
            _cachedModelPathKey = modelPathKey;
            _cachedPath = configPath;
            _cachedWriteTimeUtc = writeTimeUtc;
            _cachedLastAccessUtc = nowUtc;
            return new ConfigSnapshot(_cachedPath, _cachedConfig);
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

    internal readonly struct ConfigSnapshot
    {
        public ConfigSnapshot(string configPath, CustomAttributeConfig? config)
        {
            ConfigPath = configPath ?? string.Empty;
            Config = config;
        }

        public string ConfigPath { get; }
        public CustomAttributeConfig? Config { get; }
    }
}
