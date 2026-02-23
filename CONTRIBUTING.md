# Contributing

## Scope

This repository contains the Phase Visualizer plugin and host app for Tekla workflows.

## Workflow

1. Create a feature branch from `master`.
2. Keep changes focused and small.
3. Run a local build before opening a PR:
   - `dotnet build Plantech.Bim.sln -c Debug`
4. Open a pull request with:
   - clear summary,
   - risk notes,
   - test/validation notes.

## Coding Rules

- Prefer config-driven behavior over hardcoded per-attribute logic.
- Keep backward compatibility unless a migration plan is explicitly provided.
- Do not mix large refactoring with functional changes in one PR.
- Keep logging clear for apply/filter failures.

## Commit Messages

Use concise, intent-first messages, for example:
- `fix: ...`
- `feat: ...`
- `docs: ...`
- `refactor: ...`

## Documentation

If behavior changes, update docs in the same PR:
- `README.md`
- `Plantech.Bim.PhaseVisualizer/README.md`
- roadmap files under `Plantech.Bim.PhaseVisualizer/`
