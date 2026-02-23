using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseStatePersistenceController
{
    private readonly PhaseTableStateController _stateController;
    private readonly PhaseStateSnapshotController _snapshotController;

    public PhaseStatePersistenceController(
        PhaseTableStateController stateController,
        PhaseStateSnapshotController snapshotController)
    {
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
        _snapshotController = snapshotController ?? throw new ArgumentNullException(nameof(snapshotController));
    }

    public PhaseTableState? SaveSnapshot(
        string? stateFilePath,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        IReadOnlyCollection<PhaseTableRowState> rows,
        ILogger log)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath))
        {
            return null;
        }

        var persistedState = _stateController.Load(stateFilePath, log);
        var snapshot = _snapshotController.Build(
            persistedState,
            showAllPhases,
            useVisibleViewsForSearch,
            (rows ?? Array.Empty<PhaseTableRowState>()).ToList());

        _stateController.Save(stateFilePath, snapshot, log);
        return snapshot;
    }
}
