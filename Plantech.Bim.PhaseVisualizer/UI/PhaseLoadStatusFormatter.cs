using Plantech.Bim.PhaseVisualizer.Domain;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal static class PhaseLoadStatusFormatter
{
    public static string Build(
        int visibleRowCount,
        int objectCount,
        bool showAllPhases,
        PhaseSearchScope searchScope)
    {
        var scope = searchScope == PhaseSearchScope.VisibleViews ? "Visible views" : "Tekla model";
        return showAllPhases
            ? $"Rows: {visibleRowCount} (all phases), Objects: {objectCount}, Scope: {scope}"
            : $"Rows: {visibleRowCount}, Objects: {objectCount}, Scope: {scope}";
    }
}
