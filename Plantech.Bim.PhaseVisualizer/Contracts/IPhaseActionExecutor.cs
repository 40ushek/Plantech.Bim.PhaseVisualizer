using Plantech.Bim.PhaseVisualizer.Domain;
using System.Collections.Generic;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Contracts;

internal interface IPhaseActionExecutor
{
    PhaseApplyResult Select(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null);
    PhaseApplyResult ShowOnly(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null);
    PhaseApplyResult Visualize(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null);
}

