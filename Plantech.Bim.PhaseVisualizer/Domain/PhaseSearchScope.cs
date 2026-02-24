namespace Plantech.Bim.PhaseVisualizer.Domain;

internal enum PhaseSearchScope
{
    TeklaModel = 0,
    VisibleViews = 1,
}

internal static class PhaseSearchScopeMapper
{
    public static PhaseSearchScope FromUseVisibleViewsFlag(bool useVisibleViewsForSearch)
    {
        return useVisibleViewsForSearch
            ? PhaseSearchScope.VisibleViews
            : PhaseSearchScope.TeklaModel;
    }

    public static bool ToUseVisibleViewsFlag(PhaseSearchScope searchScope)
    {
        return searchScope == PhaseSearchScope.VisibleViews;
    }
}
