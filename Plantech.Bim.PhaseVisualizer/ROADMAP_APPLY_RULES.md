# ROADMAP: Apply Rules (Config-Driven Conditions)

## Current State (2026-02-23)

Implemented:
- `applyRule` config model is added to editable columns and flows end-to-end:
  - `Configuration/PhaseColumnConfig`
  - `UI/PhaseColumnPresentation`
  - `Domain/PhaseSelectionCriteria` (`PhaseAttributeFilter`)
  - `UI/PhaseSelectionBuilder`
- Rule runtime/validation/compiler layer added under `Rules/`:
  - `ApplyRuleConfig.cs`
  - `ApplyRuleRuntime.cs`
  - `ApplyRuleValidator.cs`
  - `ApplyRuleCompiler.cs`
  - `LegacyApplyRuleMapper.cs`
- `PhaseTableConfigValidator` validates and normalizes `applyRule` with warning-only diagnostics.
- `PhaseFilterExpressionBuilder` executes rule clauses with priority:
1. `applyRule`
2. legacy alias mapping (`booleanMode`, `exclude_existing`)
3. legacy special-case (`exclude_gratings`)
4. generic fallback

Known current limit:
- `exclude_gratings` still uses dedicated legacy profile-scope branch and is not yet fully declarative in `LegacyApplyRuleMapper`.

## Objective

Move filter condition logic from hardcoded branches to JSON-defined column rules, while keeping the plugin stable and backward-compatible.

Target outcome:
- UI remains simple (checkbox/text/number input).
- Filter logic is declarative in config (`applyRule`), not hardcoded in services.
- Fail-safe behavior stays intact (invalid rule -> warning + skip, no crash).

## Why

Current state is mixed:
- Generic editable columns work.
- Legacy macro flags (`exclude_existing`, `exclude_gratings`) still use hardcoded logic.
- `booleanMode` was introduced as a tactical improvement for numeric-backed booleans.

This solves immediate needs, but not long-term maintainability.  
`applyRule` provides one unified model.

## Scope

In scope:
- New JSON rule schema per column.
- Rule validation and diagnostics.
- Rule execution in `PhaseFilterExpressionBuilder`.
- Backward compatibility from legacy flags and `booleanMode`.

Out of scope:
- Full expression language/parser like `CalcEngine` in MpdQR.
- UI formula editor.
- Breaking config changes without migration path.

## Proposed Schema (v2 extension)

Add optional field to editable columns:

```json
{
  "key": "has_booleans",
  "label": "Has Booleans",
  "type": "Boolean",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "CUSTOM.HasBooleans",
  "applyRule": {
    "onTrue":  { "field": "CUSTOM.HasBooleans", "op": "gt", "value": 0 },
    "onFalse": { "field": "CUSTOM.HasBooleans", "op": "eq", "value": 0 }
  }
}
```

String column example:

```json
{
  "key": "profile",
  "label": "Profile",
  "type": "String",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "PROFILE",
  "applyRule": {
    "onValue": { "field": "PROFILE", "op": "startsWith" }
  }
}
```

## Rule Semantics

Boolean columns:
- If value is `true` and `onTrue` exists -> apply `onTrue`.
- If value is `false` and `onFalse` exists -> apply `onFalse`.
- If branch is missing -> no condition for that branch.

String/number/integer columns:
- If input is empty -> no condition.
- If input exists and `onValue` exists -> apply `onValue` using input as parameter.
- If `onValue` is missing -> fallback to current behavior (`equals` / `in`).

Supported operators (initial set):
- `eq`, `neq`, `contains`, `notContains`, `startsWith`, `notStartsWith`, `endsWith`, `notEndsWith`, `in`
- `gt`, `gte`, `lt`, `lte`

## Compatibility Strategy

1. `applyRule` has highest priority when present.
2. `booleanMode` remains supported as legacy alias.
3. Legacy macro flags remain supported:
- `exclude_existing`
- `exclude_gratings`
4. Validator logs warnings for legacy usage and recommends `applyRule`.

Migration mapping:
- `booleanMode: "positiveNumber"` -> generated equivalent internal rule:
  - `onTrue`: `gt 0`
  - `onFalse`: `eq 0`

## Milestones

### M11.1 - Schema and Models
Status: DONE

Deliverables:
- Add `applyRule` DTO/config models.
- Keep current config valid.

### M11.2 - Validator and Diagnostics
Status: DONE

Deliverables:
- Validate `applyRule` structure and operator/value compatibility.
- Warning-only failure mode (rule skipped, app continues).

### M11.3 - Builder Integration
Status: DONE

Deliverables:
- Execute `applyRule` in `PhaseFilterExpressionBuilder`.
- Preserve current OR/AND composition semantics.

### M11.4 - Legacy Mapping Layer
Status: PARTIAL

Deliverables:
- Map legacy `booleanMode` and macro flags to internal rule path.
- No behavior regression for existing projects.

### M11.5 - Documentation and Examples
Status: DONE

Deliverables:
- Update README with rule examples.
- Add migration notes for old configs.

### M11.6 - Optional Cleanup
Status: TODO

Deliverables:
- Mark legacy-only branches as deprecated.
- Remove hardcoded branches after adoption window.

## Risks and Controls

Risk:
- Misconfigured rules can silently change filtering.

Controls:
- Strict structure validation.
- Explicit warnings in log (`phase-visualizer.log`).
- Rule-level skip on errors, never process-level failure.

Risk:
- Behavior drift versus macro defaults.

Controls:
- Keep compatibility mapping first.
- Add regression scenarios for `exclude_existing` and `exclude_gratings`.

## Acceptance Criteria

1. Any supported condition can be configured without code changes.
2. Existing configs continue to work.
3. Invalid rule never crashes app; warning is logged.
4. Macro-equivalent conditions can be represented via `applyRule`.

## Recommended Execution Order

1. Implement M11.1 + M11.2.
2. Implement M11.3 with targeted tests.
3. Add compatibility mapping (M11.4).
4. Update docs and examples (M11.5).
5. Decide on cleanup window for M11.6.
