using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Contracts;
using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Tekla.Structures.Filtering;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class TeklaPhaseViewFilterApplier : IPhaseViewFilterApplier
{
    private const string FilterName = "PT_SubsystemSelection";
    private readonly PhaseFilterExpressionBuilder _expressionBuilder;

    public TeklaPhaseViewFilterApplier()
        : this(new PhaseFilterExpressionBuilder())
    {
    }

    internal TeklaPhaseViewFilterApplier(PhaseFilterExpressionBuilder expressionBuilder)
    {
        _expressionBuilder = expressionBuilder ?? throw new ArgumentNullException(nameof(expressionBuilder));
    }

    public PhaseApplyResult ApplyByPhaseNumbers(
        IReadOnlyCollection<PhaseSelectionCriteria> selection,
        SynchronizationContext? teklaContext,
        ILogger? log = null)
    {
        // ApplyInternal has a full try/catch and never throws; default(PhaseApplyResult) is unreachable.
        return TeklaContextDispatcher.Run(
            teklaContext,
            () => ApplyInternal(selection ?? Array.Empty<PhaseSelectionCriteria>(), _expressionBuilder, log),
            log,
            noContextWarning: null,
            sendFailedWarning: "PhaseVisualizer filter apply on Tekla context failed. Falling back to direct call.")!;
    }

    private static PhaseApplyResult ApplyInternal(
        IReadOnlyCollection<PhaseSelectionCriteria> selection,
        PhaseFilterExpressionBuilder expressionBuilder,
        ILogger? log)
    {
        try
        {
            log?.Information("PhaseVisualizer filter apply started. SelectionCount={Count}", selection.Count);
            var model = LazyModelConnector.ModelInstance;
            if (model == null)
            {
                log?.Warning("PhaseVisualizer filter apply skipped: model is not available.");
                return PhaseApplyResult.Failure(
                    PhaseApplyFailureReason.ModelUnavailable,
                    "Model is not available.");
            }

            var filterExpressions = expressionBuilder.Build(selection, out var diagnostics);
            log?.Information("PhaseVisualizer filter expressions built. Count={Count}", filterExpressions.Count);
            foreach (var diagnostic in diagnostics)
            {
                log?.Warning("PhaseVisualizer filter criteria: {Diagnostic}", diagnostic);
            }

            if (filterExpressions.Count == 0)
            {
                if (selection.Count == 0)
                {
                    return ClearActiveViewFilter(log);
                }

                log?.Warning("PhaseVisualizer filter apply skipped: no valid criteria were produced, active view filter unchanged.");
                return PhaseApplyResult.Failure(
                    PhaseApplyFailureReason.NoValidCriteria,
                    "No valid criteria were produced.");
            }

            var filterPath = GetFilterPathWithoutExtension(model);
            if (string.IsNullOrWhiteSpace(filterPath))
            {
                log?.Warning("PhaseVisualizer filter apply skipped: model path is not available.");
                return PhaseApplyResult.Failure(
                    PhaseApplyFailureReason.FilterPathUnavailable,
                    "Model path is not available.");
            }

            var filter = new Filter(filterExpressions);
            filter.CreateFile(FilterExpressionFileType.OBJECT_GROUP_VIEW, filterPath);
            log?.Information("PhaseVisualizer filter file written. Path={FilterPath}.SObjGrp", filterPath);

            var result = ApplyFilterToActiveView(log);
            log?.Information(
                "PhaseVisualizer filter apply finished. Success={Success} Reason={Reason} Detail={Detail}",
                result.IsSuccess,
                result.FailureReason,
                result.Detail);
            return result;
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer filter apply failed.");
            return PhaseApplyResult.Failure(
                PhaseApplyFailureReason.UnexpectedError,
                ex.Message);
        }
    }

    private static string? GetFilterPathWithoutExtension(Model model)
    {
        var modelPath = model.GetInfo()?.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        var attributesPath = Path.Combine(modelPath, "attributes");
        Directory.CreateDirectory(attributesPath);
        return Path.Combine(attributesPath, FilterName);
    }

    private static PhaseApplyResult ApplyFilterToActiveView(ILogger? log)
    {
        var activeView = TeklaViewHelper.GetActiveView();
        if (activeView != null)
        {
            activeView.ViewFilter = FilterName;
            activeView.Modify();
            return PhaseApplyResult.Success("Applied to active view.");
        }

        var applied = false;
        var visibleViews = ViewHandler.GetVisibleViews();
        if (visibleViews != null)
        {
            while (visibleViews.MoveNext())
            {
                if (visibleViews.Current is not View view)
                {
                    continue;
                }

                view.ViewFilter = FilterName;
                view.Modify();
                applied = true;
            }
        }

        if (!applied)
        {
            log?.Warning("PhaseVisualizer filter apply skipped: no active or visible view.");
            return PhaseApplyResult.Failure(
                PhaseApplyFailureReason.NoActiveOrVisibleView,
                "No active or visible view.");
        }

        return PhaseApplyResult.Success("Applied to visible views.");
    }

    private static PhaseApplyResult ClearActiveViewFilter(ILogger? log)
    {
        var activeView = TeklaViewHelper.GetActiveView();
        if (activeView != null)
        {
            if (string.IsNullOrEmpty(activeView.ViewFilter))
            {
                return PhaseApplyResult.Success("Filter was already cleared on active view.");
            }

            activeView.ViewFilter = string.Empty;
            activeView.Modify();
            return PhaseApplyResult.Success("Filter cleared on active view.");
        }

        var cleared = false;
        var visibleViews = ViewHandler.GetVisibleViews();
        if (visibleViews != null)
        {
            while (visibleViews.MoveNext())
            {
                if (visibleViews.Current is not View view)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(view.ViewFilter))
                {
                    cleared = true;
                    continue;
                }

                view.ViewFilter = string.Empty;
                view.Modify();
                cleared = true;
            }
        }

        if (!cleared)
        {
            log?.Warning("PhaseVisualizer filter clear skipped: no active or visible view.");
            return PhaseApplyResult.Failure(
                PhaseApplyFailureReason.NoActiveOrVisibleView,
                "No active or visible view.");
        }

        return PhaseApplyResult.Success("Filter cleared on visible views.");
    }

}

