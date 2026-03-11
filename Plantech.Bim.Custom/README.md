# Plantech.Bim.Custom

JSON-driven Tekla custom property plugins with a small interactive host for fast debugging.

## What this project is

`Plantech.Bim.Custom` is a test and pilot project for Tekla custom properties.

The current custom property is:

- `CUSTOM.PT.Filtered01`

Its purpose is simple:

- Tekla passes an object id to the custom property
- the plugin loads runtime settings from `filtered01.json`
- the plugin checks whether the object matches the configured rule
- the plugin returns an integer result such as `1` or `0`

The project supports two rule styles:

- match by Tekla filter file (`teklaFilterName`)
- match by report property value (`reportProperty` + `expectedValue`)

## Project layout

- `Plantech.Bim.Custom`
  The custom property library loaded by Tekla.
- `Plantech.Bim.Custom.Host`
  A lightweight interactive executable for debugging the same logic without deploying the custom DLL into Tekla.

## Runtime flow

For `CUSTOM.PT.Filtered01`, the runtime flow is:

1. Tekla calls `GetIntegerProperty(objectId)`.
2. The plugin resolves the Tekla model object by id.
3. The plugin loads a cached runtime config snapshot from `filtered01.json`.
4. If `teklaFilterName` is configured, the plugin resolves the `.SObjGrp` filter, caches its `FilterExpression`, and checks the current object with `Operation.ObjectMatchesToFilter(...)`.
5. Otherwise, the plugin reads the configured report property and compares it with `expectedValue`.
6. The plugin returns `trueValue` or `falseValue`.

## Config file lookup

`filtered01.json` is resolved from the first existing path:

1. `<ModelPath>/attributes/PT_PhaseVisualizer/filtered01.json`
2. `<ModelPath>/PT_PhaseVisualizer/filtered01.json`
3. `<XS_FIRM>/PT_PhaseVisualizer/filtered01.json`
4. `<ApplicationBase>/PT_PhaseVisualizer/filtered01.json`

If `XS_FIRM` contains multiple paths, all configured directories are checked in order.

## Filter file lookup

`teklaFilterName` is resolved using the same style as the Phase Visualizer filter lookup:

1. absolute path, optionally without `.SObjGrp`
2. `<ModelPath>/attributes`
3. `<ModelPath>`
4. `XS_PROJECT`
5. `XS_FIRM`
6. `XS_SYSTEM`

If the filter name has no extension, `.SObjGrp` is appended automatically during lookup.

## Config schema

Example:

```json
{
  "teklaFilterName": "standard.SObjGrp",
  "reportProperty": "ZONE",
  "expectedValue": "10",
  "trueValue": 1,
  "falseValue": 0,
  "ignoreCase": true
}
```

Fields:

- `teklaFilterName`
  Optional. If set, filter-based matching is used.
- `reportProperty`
  Optional fallback when `teklaFilterName` is not set.
- `expectedValue`
  Value compared against `reportProperty`.
- `trueValue`
  Returned when the object matches.
- `falseValue`
  Returned when the object does not match.
- `ignoreCase`
  Controls string comparison for `reportProperty` matching.

## Cache behavior

The runtime path is optimized for large object collections.

### Model cache

- The Tekla `Model` instance is stored in a shared lazy singleton.
- The runtime does not create a new `Model` per object.

### Config cache

- Runtime config is cached in memory.
- The cache is revalidated at most once every 2 seconds.
- Revalidation checks the resolved config path and file write time.
- This avoids repeated file IO for dense object batches while still picking up changed config files.

### Filter cache

- Filter resolution path is cached in memory.
- Parsed `FilterExpression` instances are cached in memory.
- Filter path resolution and expression reload are revalidated at most once every 2 seconds.
- Object matching is evaluated against the current `ModelObject`; the runtime does not cache a full model-wide result set.

### Diagnostics separation

- Runtime evaluation does not read full config or filter file text.
- Debug diagnostics are loaded separately only for the host window.
- This keeps the plugin hot path cheaper on large collections.

## Host usage

`Plantech.Bim.Custom.Host` is intended for interactive debugging.

Flow:

1. Start Tekla Structures and open a model.
2. Run `Plantech.Bim.Custom.Host.exe`.
3. Click `Pick Object In Tekla`.
4. Pick an object in Tekla.
5. The host calls the actual plugin entry points using the picked object id.
6. The host shows:
   - object id and object type
   - plugin return values
   - resolved config path
   - resolved filter path
   - config file content
   - filter file content

The host is a debug tool. The plugin remains the runtime source of truth.

## Current implementation notes

- The current plugin is intentionally small and focused on one pilot attribute.
- The codebase is structured so that more custom properties can be added later.
- Runtime behavior and debug diagnostics are intentionally separated to protect performance.
