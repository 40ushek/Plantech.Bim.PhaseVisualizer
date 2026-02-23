using Plantech.Bim.PhaseVisualizer.Contracts;
using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseActionExecutor :
    IPhaseActionExecutor
{
    private readonly IPhaseViewFilterApplier _viewFilterApplier;

    public PhaseActionExecutor()
        : this(new TeklaPhaseViewFilterApplier())
    {
    }

    public PhaseActionExecutor(IPhaseViewFilterApplier viewFilterApplier)
    {
        _viewFilterApplier = viewFilterApplier ?? throw new ArgumentNullException(nameof(viewFilterApplier));
    }

    public PhaseApplyResult Select(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null)
    {
        return ExecuteFilterAction("Select", selection, teklaContext);
    }

    public PhaseApplyResult ShowOnly(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null)
    {
        return ExecuteFilterAction("ShowOnly", selection, teklaContext);
    }

    public PhaseApplyResult Visualize(IReadOnlyCollection<PhaseSelectionCriteria> selection, SynchronizationContext? teklaContext = null)
    {
        return ExecuteFilterAction("Visualize", selection, teklaContext);
    }

    private PhaseApplyResult ExecuteFilterAction(
        string actionName,
        IReadOnlyCollection<PhaseSelectionCriteria> selection,
        SynchronizationContext? teklaContext)
    {
        var log = Serilog.Log.Logger.ForContext<PhaseActionExecutor>();
        var safeSelection = selection ?? Array.Empty<PhaseSelectionCriteria>();
        log.Information("PhaseVisualizer {ActionName} requested. Phases={Count}", actionName, safeSelection.Count);
        var result = _viewFilterApplier.ApplyByPhaseNumbers(
            safeSelection,
            teklaContext,
            log);
        log.Information(
            "PhaseVisualizer {ActionName} result. Success={Success} Reason={Reason} Detail={Detail}",
            actionName,
            result.IsSuccess,
            result.FailureReason,
            result.Detail);
        return result;
    }
}

