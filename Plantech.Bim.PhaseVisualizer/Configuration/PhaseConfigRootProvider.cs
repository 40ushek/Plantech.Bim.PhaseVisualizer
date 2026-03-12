using System;
using System.Collections.Generic;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal interface IPhaseConfigRootProvider
{
    IReadOnlyList<string> GetFirmRootDirectories();
}

internal sealed class TeklaPhaseConfigRootProvider : IPhaseConfigRootProvider
{
    public IReadOnlyList<string> GetFirmRootDirectories()
    {
        var result = new List<string>();

        try
        {
            var rawFirmPath = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XS_FIRM", ref rawFirmPath);
            if (string.IsNullOrWhiteSpace(rawFirmPath))
            {
                return result;
            }

            foreach (var firmPathToken in rawFirmPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var firmPath = firmPathToken.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(firmPath))
                {
                    result.Add(firmPath);
                }
            }
        }
        catch
        {
            return result;
        }

        return result;
    }
}
