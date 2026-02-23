namespace Plantech.Bim.PhaseVisualizer.UI;

internal static class PhaseLoadStatusFormatter
{
    public static string Build(
        int visibleRowCount,
        int objectCount,
        bool showAllPhases,
        bool useVisibleViewsForSearch)
    {
        var scope = useVisibleViewsForSearch ? "Visible views" : "Tekla model";
        return showAllPhases
            ? $"Rows: {visibleRowCount} (all phases), Objects: {objectCount}, Scope: {scope}"
            : $"Rows: {visibleRowCount}, Objects: {objectCount}, Scope: {scope}";
    }
}
