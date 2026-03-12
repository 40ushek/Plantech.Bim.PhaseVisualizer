# ROADMAP: Phase Visualizer

## Current State (2026-03-04)

Implemented:
- Dynamic WPF phase table from JSON config.
- Two UI modes:
  - host window
  - Tekla plugin window (`InputObjectDependency.NOT_DEPENDENT`) with shared `PhaseVisualizerView`.
- Tekla-native filtering via generated `OBJECT_GROUP_VIEW` (`PT_SubsystemSelection`).
- Fast phase loading:
  - phases from `model.GetPhases()`
  - per-phase object count via `GetObjectsByFilter(...).GetSize()`
  - no full-object scan in default mode.
- Conditional attribute scan only when model attributes are required.
- Toggle: show all phases vs only phases with objects.
- ViewModel keeps cached full-phase context; `Show All Phases` switch does not force repeated heavy model reads each toggle.
- State and presets persisted in `<ModelPath>/attributes/phase-visualizer.state.json`.
- Config search path migrated to:
  1. Model root: `PT_PhaseVisualizer`
  2. Firm root (`XS_FIRM`): `PT_PhaseVisualizer`
  3. Application base: `PT_PhaseVisualizer`
  4. embedded defaults.
- Logging configured to `phase-visualizer.log` in effective config directory (`PT_PhaseVisualizer`) via `PhaseVisualizerLogConfigurator`.
- Apply diagnostics are available both in UI status and logs.
- Apply now returns structured result with failure reason/details (`PhaseApplyResult`), and UI status is mapped from explicit failure reason.
- Apply criteria collection extracted from ViewModel into `UI/PhaseSelectionBuilder.cs`.
- Config-driven `applyRule` conditions introduced for editable columns (with legacy compatibility mapping).
  Detailed rollout/status is tracked in `ROADMAP_APPLY_RULES.md`.
- Config-driven Tekla filter reference for editable boolean columns:
  - `teklaFilterName` is supported in column schema.
  - referenced `.SObjGrp` filter expression is loaded and merged into the phase branch where the toggle is enabled.
  - unresolved/invalid file is logged as warning and ignored (fail-safe).
  - relative filter name lookup now uses Tekla attribute directories:
    1. `<ModelPath>/attributes`
    2. `<ModelPath>`
    3. `XS_PROJECT`
    4. `XS_FIRM`
    5. `XS_SYSTEM`
  - diagnostics include found/not-found, source folder, and candidate/probe paths.
- Namespace simplified to `Plantech.Bim.PhaseVisualizer.*`.
- `Bolt` object type supported: display columns (`objectType: Bolt`) and editable filter columns (`targetObjectType: Bolt`).
  `BoltGroup` objects collected alongside `Part` in the attribute scan; report properties read via `GetReportProperty`.

## Architecture (Locked)

Decision:
- Model columns are explicit: `objectType + attribute`.
- Editable criteria columns are explicit: `editable: true` plus target mapping.
- No semantic `source` routing (`input.profile`, `input.material`, etc.).

Rationale:
- Removes per-attribute hardcode.
- Removes column-name matching logic.
- New filterable columns can be added by config only.

Current editable schema:
- `editable: true`
- For model-targeted filters: `targetObjectType + targetAttribute`
- For built-in criteria flags: `targetAttribute` only (for example `exclude_gratings`, `exclude_existing`).
- For Tekla file filters: `teklaFilterName` (boolean toggle column, phase-local inside the selected phase branch).

Supported `objectType` / `targetObjectType` values:

| Value | Tekla type | Attribute source |
|---|---|---|
| `Phase` | `Phase` | `number`, `name` — fixed set |
| `Part` | `Part` | `profile`, `material`, `class`, `name`, `finish`, `ua.<name>` |
| `Assembly` | `Assembly` | any Tekla report property (e.g. `ASSEMBLY.MAINPART.PROFILE`) — passed through |
| `Bolt` | `BoltGroup` | any Tekla report property (e.g. `BOLT_STANDARD`) — passed through |

## Milestone Status

### M1 - Expression Builder Extraction
Status: DONE

### M2 - Typed Operator Model (Config-Driven)
Status: DONE

### M3 - Rule Validation and Diagnostics
Status: DONE

### M4 - Presets UX
Status: DONE

### M5 - Source Mapping and Config Strictness
Status: DONE

Done:
- Legacy semantic sources rejected by validator for model columns.
- Resolver uses explicit object/attribute mapping.
- Config directories unified to `PT_PhaseVisualizer` for model / firm (`XS_FIRM`) / application roots.

### M6 - Hardening and Test Coverage
Status: TODO

Planned:
- Integration checks for phase add/remove/rename.
- Recovery checks for malformed state/config.
- Regression coverage for expression generation.
- Performance checks on large models.

### M7 - Universal Editable Criteria (No Attribute Hardcode)
Status: DONE

Done:
- `editable` flag added to column schema.
- ViewModel no longer depends on `input.*` or fixed column keys for normal attribute filters.
- Apply path now processes all editable columns with `targetObjectType + targetAttribute`.
- `material` (and other same-shape attributes) works without code changes.

### M8 - Typed Editable Values and Operation Semantics
Status: TODO

Planned:
- Expand generic builder for typed editable filters beyond string-equivalent flow where needed.
- Map boolean/number editable values to explicit Tekla operators where target supports it.
- Add config-level per-column operation policy for editable targets.

### M9 - Observability and Apply Diagnostics
Status: DONE

Done:
- Log file initialization and write path resolved from effective config directory.
- Apply chain emits diagnostics (`ShowOnly requested/result`, filter apply start/finish, expression build count).
- UI shows explicit failure status (`Apply failed...`) when apply returns `false`.

### M11 - Model Scope vs View Scope Separation
Status: DONE

Done:
- Internal runtime flow now uses explicit `PhaseSearchScope` (model vs visible views) instead of implicit bool in load orchestration.
- Data-load context cache is keyed by `PhaseSearchScope`, making scope separation explicit in controller/workflow/provider chain.
- Status/UI load formatting now receives explicit scope enum, not boolean flag.
- Phase counts remain model-wide (`GetObjectsByFilter(...).GetSize()`), independent of view scope.
- Visualization/apply remains view-level (active/visible view), separated from data query scope.

### M16 - Bolt Object Type
Status: DONE

Done:
- `PhaseColumnObjectType.Bolt` added to enum.
- `PhaseSourceResolver`: `Bolt` case in `TryBuildModelSource` (source = `bolt.<attr>`) and `TryGetTemplateStringField` (attribute passes through, preserving casing — same pattern as `Assembly`).
- `TeklaPhaseDataProvider`: `RequiredSourceSet.BoltAttributes`; `BoltGroup` objects collected alongside `Part` in the attribute scan loop; `BuildBoltRecord` reads phase + `GetReportProperty` per requested attribute.
- `PhaseTableConfigValidator`: `Bolt` allowed as `targetObjectType` for editable filter columns (alongside `Part` and `Assembly`).
- Bug fix: `TryResolveTemplateStringField` was lowercasing `targetAttribute` before passing to `PhaseSourceResolver`, breaking Assembly and Bolt template field names. Removed the unnecessary `ToLowerInvariant()`; normalization is handled inside `PhaseSourceResolver` per object type.

### M17 - Tekla Filter Reference Columns
Status: DONE

Done:
- `teklaFilterName` added to column config/presentation/selection pipeline.
- Validator normalization added:
  - accepted only for editable boolean columns,
  - unsupported usage is ignored with warning.
- `PhaseFilterExpressionBuilder` loads referenced `.SObjGrp` and combines it inside the corresponding phase group.
- `teklaFilterNegate` option added for file-filter columns (optional; default `false`) to invert loaded file filter before merge.
- Filter reference resolution:
  - absolute file path, or
  - relative name under Tekla attribute directories (`<ModelPath>/attributes`, `<ModelPath>`, `XS_PROJECT`, `XS_FIRM`, `XS_SYSTEM`),
  - `.SObjGrp` extension is auto-appended when omitted.
- File filter branch is applied only for truthy boolean value in that editable column.
- Fail-safe behavior:
  - missing/unresolvable/invalid Tekla filter is logged and skipped,
  - apply flow continues without crash.

### M15 - ApplyRule Config-Driven Conditions
Status: DONE

Done:
- `applyRule` schema + pass-through + validator + builder execution are implemented.
- Legacy alias mapping for `booleanMode` and `exclude_existing` is implemented.
- `exclude_gratings` moved from builder special-case into declarative legacy mapping.
- Focused regression checks added for legacy no-op/true-branch behavior (`exclude_existing`, `exclude_gratings`) and generic fallback diagnostics.

## Known Behavior / Known Limits

- `Count` is model-level per-phase count (phase filter + `GetSize()`), not "visible-only in current view". Values can differ from objects currently visible in view windows.
- `Apply` requires an active view or at least one visible view in Tekla; otherwise apply returns `false`.
- Attribute scan path for model columns currently uses visible views (`GetVisibleViews` + `GetObjectsByBoundingBox` fallback to active view), so some table values can still be view-dependent.
- In Tekla version mismatch scenarios (for example plugin built with TS2021 API and loaded in TS2025), active-view access can fail at runtime; this is now visible in logs via structured apply failure details.
- Some template/system filters (for example certain `standard.SObjGrp` variants with empty template operands) may fail to parse via `Filter(fullFileName, ...)`; such filters are currently logged and ignored in composed apply expression.
- `teklaFilterName` currently supports object-group selection filters (`.SObjGrp`) only; representation/view filters are intentionally out of scope for this pipeline.
- Multi-phase selection still uses a root `OR` collection over phase groups. Keep this for now because the generated Tekla filter is working correctly; only revisit root-level flattening if a real bracket-depth limit is hit in Tekla.

Note:
- Refactoring-only tasks are tracked separately in local file `ROADMAP_REFACTORING.local.md` (not in Git).

## Guiding Principles

- Keep filtering Tekla-native; avoid full in-memory filtering.
- Keep config and runtime state separate:
  - `phase-visualizer.json` = schema/layout/targets
  - `phase-visualizer.state.json` = user row values and presets.
- Bind persisted row state by `PhaseNumber` (phase name is not stable).

## Next Recommended Step

1. Complete **M6 hardening + tests** with focus on `Apply` stability and logging diagnostics.
2. Execute **M8 typed editable operations** if priority remains unchanged.

## Future UX Idea

- Support multiple config profiles in `PT_PhaseVisualizer`, for example `default.phase-visualizer.json`, `production.phase-visualizer.json`, `erection.phase-visualizer.json`.
- Add a `ComboBox` in the UI that lists profile names without the technical file suffix.
- When the active profile changes, fully reload the table from the selected config and use state scoped to that config.
- Remember the last selected profile for the current user.
