# Plantech.Bim.Custom

Tekla custom property plugins with JSON-driven rules.

## Config lookup

`Filtered` reads `filtered01.json` from the first existing path:

1. `<ModelPath>/attributes/PT_Custom/filtered01.json`
2. `<ModelPath>/PT_Custom/filtered01.json`
3. `<XS_FIRM>/PT_Custom/filtered01.json`
4. `<ApplicationBase>/PT_Custom/filtered01.json`

## Example

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

For `CUSTOM.PT.Filtered01`:

- if `teklaFilterName` is set, the plugin resolves the Tekla filter file and returns `trueValue` when the object is included in that filter;
- otherwise it falls back to `reportProperty` + `expectedValue`.

`teklaFilterName` is resolved like in `PhaseVisualizer`:

1. absolute path, optionally without `.SObjGrp`
2. `<ModelPath>/attributes`
3. `<ModelPath>`
4. `XS_PROJECT`
5. `XS_FIRM`
6. `XS_SYSTEM`

## Host

`Plantech.Bim.Custom.Host` is a small interactive WPF executable for testing without loading the custom DLL into Tekla.

Flow:

1. start Tekla and open a model
2. run `Plantech.Bim.Custom.Host.exe`
3. click `Pick Object In Tekla`
4. pick an object in Tekla
5. inspect the evaluated result, config path, resolved filter path, and returned integer value
