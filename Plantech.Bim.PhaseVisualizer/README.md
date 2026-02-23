# Plantech.Bim.PhaseVisualizer

Tekla phase visualization tool with config-driven columns and Tekla-native view filtering.

## Plugin Mode

- Tekla plugin mode is supported via `PluginWindowBase`.
- Host app mode and plugin mode use the same shared UI control (`UI/PhaseVisualizerView`).

## Config Location

Config file name: `phase-visualizer.json`

Load order:
1. `<ModelPath>/.plantech/phase-visualizer.json`
2. `<ExtensionRoot>/.plantech/phase-visualizer.json`
3. embedded defaults

## Logging

- Log file name: `phase-visualizer.log`
- Log directory resolution order:
1. `<ModelPath>/.plantech`
2. `<ExtensionRoot>/.plantech`
3. `%LOCALAPPDATA%/Plantech/PhaseVisualizer`

## Column Model

### Model column (read from model)

Use explicit `objectType + attribute`:

```json
{
  "key": "phase_name",
  "label": "Name",
  "type": "String",
  "objectType": "Phase",
  "attribute": "name",
  "aggregate": "First",
  "visibleByDefault": true,
  "order": 20,
  "filterOps": ["equals", "contains", "in"]
}
```

### Editable filter column (user input)

Use `editable: true` and target mapping:

```json
{
  "key": "material",
  "label": "Material",
  "type": "String",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "material",
  "aggregate": "First",
  "visibleByDefault": true,
  "order": 25,
  "filterOps": ["equals", "contains", "in"]
}
```

For boolean columns backed by numeric custom properties (`0=false`, `>0=true`), use:

```json
{
  "key": "has_booleans",
  "label": "Has Booleans",
  "type": "Boolean",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "CUSTOM.HasBooleans",
  "booleanMode": "positiveNumber",
  "filterOps": ["equals"]
}
```
Behavior for `booleanMode: "positiveNumber"`:
- checked (`true`) -> `targetAttribute > 0`
- unchecked (`false`) -> `targetAttribute = 0`

### Config-driven rules (`applyRule`)

You can define filter logic in config without adding code branches:

```json
{
  "key": "has_booleans",
  "label": "Has Booleans",
  "type": "Boolean",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "CUSTOM.HasBooleans",
  "applyRule": {
    "onTrue":  { "op": "gt", "value": 0 },
    "onFalse": { "op": "eq", "value": 0 }
  }
}
```

```json
{
  "key": "profile",
  "label": "Profile",
  "type": "String",
  "editable": true,
  "targetObjectType": "Part",
  "targetAttribute": "PROFILE",
  "applyRule": {
    "onValue": { "op": "startsWith" }
  }
}
```

Notes:
- `field` is optional in rule clauses. If omitted, `targetAttribute` is used.
- If clause `value` is omitted, user input from the cell is used.
- Supported ops: `eq`, `neq`, `in`, `contains`, `notContains`, `startsWith`, `notStartsWith`, `endsWith`, `notEndsWith`, `gt`, `gte`, `lt`, `lte`.

## Apply Behavior

- `Apply` always filters by selected phases.
- All editable columns with `targetObjectType + targetAttribute` and non-empty values are included in generated Tekla filter.
- Rule priority for editable columns:
1. `applyRule`
2. legacy alias mapping (`booleanMode: "positiveNumber"`, `exclude_existing`)
3. legacy special branch (`exclude_gratings`)
4. generic fallback (`equals` / `in`) for model-targeted editable fields
- `applyRule` is fail-safe: invalid branch is skipped with warning, app does not crash.
- Built-in flags (`exclude_gratings`, `exclude_existing`) are handled as criteria flags via `targetAttribute`.
- `exclude_gratings` and `exclude_existing` are legacy macro-era flags; prefer explicit editable model-targeted columns for new configs.
- `Apply` requires an active view or at least one visible view in Tekla.
- `Count` is calculated at model level per phase, so it can be greater than objects currently visible in a specific view.

## Apply Troubleshooting

- UI message `Apply failed. Check active/visible view and criteria.` means filter apply returned `false`.
- Check `phase-visualizer.log` and search for these entries:
1. `PhaseVisualizer ShowOnly requested`
2. `PhaseVisualizer filter apply started`
3. `PhaseVisualizer filter expressions built`
4. `PhaseVisualizer filter apply finished`
5. `PhaseVisualizer ShowOnly result`
- If apply failed, also check warnings like:
1. `no active or visible view`
2. `no valid criteria were produced`
3. `filter apply failed`

## State and Presets

- Runtime state and presets are stored in:
  - `<ModelPath>/attributes/phase-visualizer.state.json`
- State binds row values by `PhaseNumber`.

## Notes

- Legacy semantic sources (`input.profile`, `input.material`, etc.) are not required.
- Preferred schema is explicit and declarative (`editable + target*` for user-entered filters).
