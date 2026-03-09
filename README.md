# Plantech BIM Tools

Tekla Structures tooling for configuration-driven visualization and custom attributes.

## What Is Included

- `Plantech.Bim.PhaseVisualizer` - main library/plugin logic.
- `Plantech.Bim.PhaseVisualizer.Host` - host app for standalone testing.
- `Plantech.Bim.Custom` - JSON-driven custom attribute prototype for Tekla.
- `Plantech.Bim.Custom.Host` - interactive host for custom attribute debugging and object picking.

The tool supports:
- host window mode,
- Tekla plugin window mode,
- JSON-driven table/filters (`PT_PhaseVisualizer/phase-visualizer.json`),
- generated Tekla view filters (`PT_SubsystemSelection`).

`Plantech.Bim.Custom` currently includes:
- `CUSTOM.PT.Filtered01`
- JSON-based runtime config (`filtered01.json`)
- Tekla filter-based matching through `.SObjGrp`
- report property fallback matching
- a debug host that calls the real plugin entry points for a picked object id

## Quick Start

1. Build solution:
   - `dotnet build Plantech.Bim.sln -c Debug`
2. Configure columns/filters:
   - `Plantech.Bim.PhaseVisualizer.Host/bin/Debug/net48/PT_PhaseVisualizer/phase-visualizer.json`
3. Run host mode for fast UI iteration, or load plugin in Tekla.

## Config and Logs

- Config search order:
  1. `<ModelPath>/PT_PhaseVisualizer/phase-visualizer.json`
  2. `<XS_FIRM>/PT_PhaseVisualizer/phase-visualizer.json`
  3. `<ApplicationBase>/PT_PhaseVisualizer/phase-visualizer.json`
  4. embedded defaults
- Log file:
  - `phase-visualizer.log` in effective `PT_PhaseVisualizer` directory

- `teklaFilterName` lookup for relative names:
  1. `<ModelPath>/attributes`
  2. `<ModelPath>`
  3. `XS_PROJECT` directories
  4. `XS_FIRM` directories
  5. `XS_SYSTEM` directories
  - absolute path is also supported
  - `.SObjGrp` is auto-appended when extension is omitted

## Documentation

Detailed documentation and behavior notes:
- `Plantech.Bim.PhaseVisualizer/README.md`
- `Plantech.Bim.PhaseVisualizer/ROADMAP_PHASE_VISUALIZER.md`
- `Plantech.Bim.PhaseVisualizer/ROADMAP_APPLY_RULES.md`
- `Plantech.Bim.Custom/README.md`

Repository-level docs:
- `CONTRIBUTING.md`
- `CHANGELOG.md`
- `LICENSE`
