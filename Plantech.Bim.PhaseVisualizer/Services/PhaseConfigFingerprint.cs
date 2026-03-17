using System;
using System.IO;
using System.Security.Cryptography;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal static class PhaseConfigFingerprint
{
    public static string ComputeFromFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }
}
