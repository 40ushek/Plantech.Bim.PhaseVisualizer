# Plantech.Bim.PhaseVisualizer

Tekla Structures phase visualizer with config-driven columns and view filtering.

## What Is Included

- `Plantech.Bim.PhaseVisualizer` - main library/plugin logic.
- `Plantech.Bim.PhaseVisualizer.Host` - host app for standalone testing.

The tool supports:
- host window mode,
- Tekla plugin window mode,
- JSON-driven table/filters (`.plantech/phase-visualizer.json`),
- generated Tekla view filters (`PT_SubsystemSelection`).

## Quick Start

1. Build solution:
   - `dotnet build Plantech.Bim.sln -c Debug`
2. Configure columns/filters:
   - `Plantech.Bim.PhaseVisualizer.Host/bin/Debug/net48/.plantech/phase-visualizer.json`
3. Run host mode for fast UI iteration, or load plugin in Tekla.

## Config and Logs

- Config search order:
  1. `<ModelPath>/.plantech/phase-visualizer.json`
  2. `<ExtensionRoot>/.plantech/phase-visualizer.json`
  3. embedded defaults
- Log file:
  - `phase-visualizer.log` in effective `.plantech` directory

## Documentation

Detailed documentation and behavior notes:
- `Plantech.Bim.PhaseVisualizer/README.md`
- `Plantech.Bim.PhaseVisualizer/ROADMAP_PHASE_VISUALIZER.md`
- `Plantech.Bim.PhaseVisualizer/ROADMAP_APPLY_RULES.md`

