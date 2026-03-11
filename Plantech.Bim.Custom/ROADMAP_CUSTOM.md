# Plantech.Bim.Custom Roadmap

## Current State

- `CUSTOM.PT.Filtered01` is implemented as a pilot custom property.
- Runtime settings are loaded from `filtered01.json` in the shared `PT_PhaseVisualizer` directory.
- Filter matching uses `Operation.ObjectMatchesToFilter(...)` with cached filter path resolution.
- The host project is available for interactive debugging against a live Tekla model.

## Near-Term Direction

- Keep the runtime path small and predictable.
- Reuse cached config and filter data where it improves throughput.
- Keep diagnostics and host-only behavior outside the plugin hot path.

## Out of Scope For Now

- Multiple custom properties in one config file.
- Rich rule composition.
- Broad framework or packaging changes.
