# Plantech.Bim.PhaseVisualizer

Tekla phase visualization tool with config-driven columns and Tekla-native view filtering.

## Plugin Mode

- Tekla plugin mode is supported via `PluginWindowBase`.
- Host app mode and plugin mode use the same shared UI control (`UI/PhaseVisualizerView`).

## Config Location

Config file name: `phase-visualizer.json`

Load order:
1. Model root:
   - `<ModelPath>/PT_PhaseVisualizer/phase-visualizer.json`
2. Firm root (`XS_FIRM`):
   - `<XS_FIRM>/PT_PhaseVisualizer/phase-visualizer.json`
3. Application base:
   - `<ApplicationBase>/PT_PhaseVisualizer/phase-visualizer.json`
4. embedded defaults

If no config file exists in any lookup path, a default config is auto-created at:
- `<ModelPath>/PT_PhaseVisualizer/phase-visualizer.json`
- Auto-created default config uses explicit `applyRule` mappings (no legacy `targetAttribute=exclude_*` flags).

## Logging

- Log file name: `phase-visualizer.log`
- Log file is reset on each new window open (host and plugin).
- Startup diagnostics log includes:
  - `Model`, `Firm`, `Environment`
  - `ConfigFile`, `ConfigPath`, `ConfigSource`
  - `ConfigProbePaths` (full candidate chain used for lookup)
- Log directory resolution order:
1. `<ModelPath>/PT_PhaseVisualizer`
2. `<XS_FIRM>/PT_PhaseVisualizer`
3. `<ApplicationBase>/PT_PhaseVisualizer`
4. `%LOCALAPPDATA%/Plantech/PhaseVisualizer`

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

Supported `objectType` / `targetObjectType` values:

| Value | Tekla type | Attribute source |
|---|---|---|
| `Phase` | `Phase` | `number`, `name` |
| `Part` | `Part` | `profile`, `material`, `class`, `name`, `finish`, `ua.<name>` |
| `Assembly` | `Assembly` | any Tekla report property (for example `ASSEMBLY.MAINPART.PROFILE`) |
| `Bolt` | `BoltGroup` | any Tekla report property (for example `BOLT_STANDARD`) |

Example model column for assembly:

```json
{
  "key": "assembly_main_profile",
  "label": "Assembly Main Profile",
  "type": "String",
  "objectType": "Assembly",
  "attribute": "ASSEMBLY.MAINPART.PROFILE",
  "aggregate": "First",
  "visibleByDefault": false,
  "order": 40
}
```

Example model column for bolt:

```json
{
  "key": "bolt_standard",
  "label": "Bolt Standard",
  "type": "String",
  "objectType": "Bolt",
  "attribute": "BOLT_STANDARD",
  "aggregate": "Distinct",
  "visibleByDefault": false,
  "order": 50
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

### Tekla file filter column (`teklaFilterName`)

You can attach an existing Tekla object group filter (`.SObjGrp`) to a boolean editable column:

```json
{
  "key": "standard_filter",
  "label": "Standard",
  "type": "Boolean",
  "editable": true,
  "teklaFilterName": "standard.SObjGrp",
  "teklaFilterNegate": false
}
```

Behavior:
- The file-filter branch is applied only when the column value is `true`.
- `teklaFilterName` is merged with generated phase/attribute criteria using `AND`.
- `teklaFilterNegate` is optional (default: `false` when omitted).
- If `teklaFilterNegate=true`, the loaded Tekla filter expression is inverted (`NOT`) before AND-merge.
- `teklaFilterName` can be:
  - absolute file path, or
  - relative name searched in Tekla attribute directories (in order):
    1. `<ModelPath>/attributes`
    2. `<ModelPath>`
    3. `XS_PROJECT` directories
    4. `XS_FIRM` directories
    5. `XS_SYSTEM` directories
- If extension is omitted, `.SObjGrp` is tried automatically.
- Missing or invalid filter file is logged as warning and skipped (fail-safe).
- Existing generated criteria still apply even if file filter is skipped.
- Apply diagnostics now include:
  - found/not-found status for `teklaFilterName`,
  - resolved source folder and full path when found,
  - probed folders and candidate paths when not found,
  - explicit info when filter is skipped because toggle value is `false`.

## Apply Behavior

- `Apply` always filters by selected phases.
- All editable columns with `targetObjectType + targetAttribute` and non-empty values are included in generated Tekla filter.
- Rule priority for editable columns:
1. `applyRule`
2. legacy alias mapping (`booleanMode: "positiveNumber"`, `exclude_existing`, `exclude_gratings`)
3. generic fallback (`equals` / `in`) for model-targeted editable fields
- `applyRule` is fail-safe: invalid branch is skipped with warning, app does not crash.
- Built-in flags (`exclude_gratings`, `exclude_existing`) are handled as criteria flags via `targetAttribute`.
- `exclude_gratings` and `exclude_existing` are legacy macro-era flags; prefer explicit editable model-targeted columns for new configs.
- `Apply` requires an active view or at least one visible view in Tekla.
- `Count` is calculated at model level per phase, so it can be greater than objects currently visible in a specific view.

Notes:
- There is no `AssemblyMainPart` object type in schema anymore; use `Assembly`.
- Model columns for `Part` are intentionally strict (`profile/material/class/name/finish` and `ua.<name>`). `Assembly` and `Bolt` use pass-through report property names.

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
