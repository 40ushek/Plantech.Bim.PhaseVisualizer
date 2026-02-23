using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System.Collections.Generic;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Contracts;

internal interface IPhaseViewFilterApplier
{
    PhaseApplyResult ApplyByPhaseNumbers(
        IReadOnlyCollection<PhaseSelectionCriteria> selection,
        SynchronizationContext? teklaContext,
        ILogger? log = null);
}

