# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Plantech.Bim.PhaseVisualizer** is a Tekla Structures plugin (C# / .NET 4.8 / WPF) that provides phase-based visualization and filtering of BIM model objects. It ships as a DLL loaded by Tekla Structures at runtime.

## Build Commands

This is a Visual Studio / MSBuild project. There is no Makefile or npm.

```bash
# Build (from repo root or solution directory)
dotnet build Plantech.Bim.sln

# Release build
dotnet build Plantech.Bim.sln -c Release

# Single project
dotnet build Plantech.Bim.PhaseVisualizer/Plantech.Bim.PhaseVisualizer.csproj
```

There are no automated tests in the repository yet (test projects not yet added).

## Architecture

### Namespace Convention

All code lives under the `Plantech.Bim.PhaseVisualizer.*` namespace.

### Layer Structure

```
Contracts/       ← IPhaseActionExecutor, IPhaseViewFilterApplier
Domain/          ← Immutable data models and enums
Configuration/   ← Config loading, validation, defaults
Services/        ← Concrete service implementations
Orchestration/   ← PhaseVisualizerController (wires layers)
UI/              ← WPF XAML + MVVM ViewModel
Common/          ← Cross-cutting utilities (LazyModelConnector, TeklaViewHelper)
Plugins/         ← Tekla plugin entry point(s); currently empty
```

### Data Flow

```
PhaseVisualizerLauncher.Open()               # public entry point; creates controller + window directly
  → PhaseVisualizerController.LoadContext()
      → PhaseTableConfigLoader.Load()             # .plantech config → extension config → embedded defaults
      → TeklaPhaseDataProvider.LoadVisibleParts() # fast: GetPhases() + filter counts;
                                                  # full scan only when model-attribute columns present
      → PhaseTableBuilder.BuildRows()             # group by phase, apply aggregations
      → returns PhaseVisualizerContext             # config + rows + state file path + metadata
```

User action → `PhaseActionExecutor.ShowOnly(selection, teklaContext)` → `PhaseFilterExpressionBuilder` → `TeklaPhaseViewFilterApplier`.
**`Select` action is not yet implemented** (logs a warning stub).

### Tekla Integration Points

- **Data collection:** `TeklaPhaseDataProvider` has two modes:
  - *Fast (default):* phases from `model.GetPhases()`, per-phase object count via `GetObjectsByFilter(...).GetSize()` — no full object scan.
  - *Full scan:* triggered when model-attribute columns (profile, material, etc.) are present in config; collects visible `Part` objects via `ModelObjectSelector` bounding-box queries.
- **Filter building:** `PhaseFilterExpressionBuilder` translates `PhaseSelectionCriteria` into Tekla `BinaryFilterExpressionCollection` (AND within a phase group, OR across phases).
- **View filtering:** `TeklaPhaseViewFilterApplier` creates/updates a Tekla filter named `PT_SubsystemSelection` at `<model>/attributes/PT_SubsystemSelection` and applies it to the active view via `ViewFilter`. An empty phase set clears the active view filter.
- **Threading:** All calls into Tekla must be marshalled via `SynchronizationContext`; passed as a parameter through `PhaseActionExecutor.ShowOnly/Visualize(selection, teklaContext)`.
- **Model connection:** `LazyModelConnector` (singleton) lazy-loads `Model` and `ModelObjectSelector` instances.
- **Tekla API version switching:** `TeklaViewHelper` uses `#if TS2021` / `#if TS2025` conditional compilation to handle API differences (e.g., `GetActiveView`). The active symbol is `TS2021` (set in `DefineConstants` in the csproj).

### Configuration System

Configs are loaded in priority order:
1. `<model>/.plantech/phase-visualizer.json` (model-specific overrides)
2. `<extension_root>/.plantech/phase-visualizer.json` (bundled with plugin)
3. `PhaseTableConfigDefaults.Create()` (embedded C# defaults)

Config version is `2`. Legacy callers passing `<model>/.mpd/menu` are handled by a path fallback in `PhaseVisualizerController`.

**Column schema — two mutually exclusive column types:**

| Column type | When | Key fields |
|---|---|---|
| Model column (read-only display) | `editable` is false/absent | `objectType` + `attribute` |
| Editable column (filter criteria) | `editable: true` | `targetObjectType` + `targetAttribute` (for attribute filters); `targetAttribute` only for built-ins (`exclude_gratings`, `exclude_existing`) |

The legacy `source` string field (`phase.number`, `part.profile`, etc.) is still parsed by `PhaseSourceResolver` but rejected by the validator for model columns; use `objectType`+`attribute` instead.

**Aggregate types:** `Count`, `First`, `Distinct`, `Min`, `Max`

### State Persistence

User row state (selected rows, editable input values) and named presets are stored in `<model>/attributes/phase-visualizer.state.json` via `PhaseTableStateStore`. State is keyed by `PhaseNumber` (phase name is not stable). Config (`phase-visualizer.json`) and state (`phase-visualizer.state.json`) are kept strictly separate.

### Key Files

| File | Purpose |
|------|---------|
| [PhaseVisualizerLauncher.cs](Plantech.Bim.PhaseVisualizer/PhaseVisualizerLauncher.cs) | Public plugin entry point |
| [Orchestration/PhaseVisualizerController.cs](Plantech.Bim.PhaseVisualizer/Orchestration/PhaseVisualizerController.cs) | Main orchestrator |
| [Services/TeklaPhaseDataProvider.cs](Plantech.Bim.PhaseVisualizer/Services/TeklaPhaseDataProvider.cs) | Phase catalog + optional full attribute scan |
| [Services/PhaseFilterExpressionBuilder.cs](Plantech.Bim.PhaseVisualizer/Services/PhaseFilterExpressionBuilder.cs) | Builds Tekla filter expressions from PhaseSelectionCriteria |
| [Services/TeklaPhaseViewFilterApplier.cs](Plantech.Bim.PhaseVisualizer/Services/TeklaPhaseViewFilterApplier.cs) | Creates & applies Tekla view filters |
| [Services/PhaseTableStateStore.cs](Plantech.Bim.PhaseVisualizer/Services/PhaseTableStateStore.cs) | Persists user row state and presets |
| [Services/PhaseTableBuilder.cs](Plantech.Bim.PhaseVisualizer/Services/PhaseTableBuilder.cs) | Groups objects by phase, applies aggregations |
| [Configuration/PhaseTableConfigLoader.cs](Plantech.Bim.PhaseVisualizer/Configuration/PhaseTableConfigLoader.cs) | Hierarchical config loading |
| [Configuration/PhaseTableConfigDefaults.cs](Plantech.Bim.PhaseVisualizer/Configuration/PhaseTableConfigDefaults.cs) | Embedded default config |
| [Domain/PhaseSelectionCriteria.cs](Plantech.Bim.PhaseVisualizer/Domain/PhaseSelectionCriteria.cs) | Runtime filter criteria per phase |
| [Domain/PhaseSourceResolver.cs](Plantech.Bim.PhaseVisualizer/Domain/PhaseSourceResolver.cs) | Source string parsing and template field mapping |
| [UI/PhaseVisualizerViewModel.cs](Plantech.Bim.PhaseVisualizer/UI/PhaseVisualizerViewModel.cs) | MVVM ViewModel for the DataGrid window |
| [Directory.Build.Props](Directory.Build.Props) | Sets `TeklaVersion=2025.0` (Debug) for TSAppConfigPatcherTask |

### Host Project

`Plantech.Bim.PhaseVisualizer.Host` is a WinExe shell that references the main library. It exists for manual testing without a full Tekla Structures installation. Place test config at `<Host output>/.plantech/phase-visualizer.json`.

### Legacy Code

[PT_Teilsystem_Visualizer_V1.0.cs](Plantech.Bim.PhaseVisualizer/PT_Teilsystem_Visualizer_V1.0.cs) is the original monolithic WinForms macro. Do not modify it; it is excluded from compilation and exists for reference only.

## Tech Stack

- **Language:** C# 12.0, nullable reference types enabled
- **Runtime:** .NET Framework 4.8 (net48)
- **UI:** WPF + XAML (MVVM pattern)
- **Logging:** Serilog with file sink
- **JSON:** Newtonsoft.Json
- **Tekla API:** NuGet packages `Tekla.Structures` 2021.0.0; `DefineConstants=TS2021`; `TeklaVersion=2025.0` in `Directory.Build.Props` affects `TSAppConfigPatcherTask` only (not NuGet resolution)
