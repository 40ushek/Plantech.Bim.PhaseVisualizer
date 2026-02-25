using Plantech.Bim.PhaseVisualizer.Domain;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal static class PhaseLoadStatusFormatter
{
    public static string Build(
        int visibleRowCount,
        int objectCount,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool showObjectCount)
    {
        var scope = searchScope == PhaseSearchScope.VisibleViews ? "Visible views" : "Tekla model";
        if (showObjectCount)
        {
            return showAllPhases
                ? $"Rows: {visibleRowCount} (all phases), Objects: {objectCount}, Scope: {scope}"
                : $"Rows: {visibleRowCount}, Objects: {objectCount}, Scope: {scope}";
        }

        return showAllPhases
            ? $"Rows: {visibleRowCount} (all phases), Scope: {scope}"
            : $"Rows: {visibleRowCount}, Scope: {scope}";
    }
}
