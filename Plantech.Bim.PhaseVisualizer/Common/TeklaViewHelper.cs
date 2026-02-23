using Tekla.Structures.Model.UI;

namespace Plantech.Bim.PhaseVisualizer.Common;

internal static class TeklaViewHelper
{
    public static View? GetActiveView()
    {
#if TS2021
        View? activeView = null;
        Tekla.Structures.ModelInternal.Operation.dotGetCurrentView(ref activeView);
        return activeView;
#elif TS2025
        return ViewHandler.GetActiveView();
#else
        return ViewHandler.GetActiveView();
#endif
    }
}
