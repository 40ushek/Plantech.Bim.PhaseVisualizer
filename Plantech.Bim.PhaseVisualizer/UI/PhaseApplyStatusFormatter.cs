using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal static class PhaseApplyStatusFormatter
{
    public static string Build(
        PhaseApplyResult result,
        IReadOnlyCollection<PhaseSelectionCriteria> selection)
    {
        if (!result.IsSuccess)
        {
            return result.FailureReason switch
            {
                PhaseApplyFailureReason.NoActiveOrVisibleView => selection.Count == 0
                    ? "Filter clear failed. No active or visible view."
                    : "Apply failed. No active or visible view.",
                PhaseApplyFailureReason.NoValidCriteria => "Apply failed. No valid criteria from selected rows.",
                PhaseApplyFailureReason.ModelUnavailable => "Apply failed. Tekla model is not available.",
                PhaseApplyFailureReason.FilterPathUnavailable => "Apply failed. Model path is not available.",
                _ => "Apply failed. Check active/visible view and criteria.",
            };
        }

        return selection.Count == 0
            ? "Filter cleared on active view."
            : $"Applied by {selection.Count} phase(s).";
    }
}
