# Implementation plan

## Remediation goal

Model spatial semantics explicitly as canonical node-owned data (carrier or spatial facet), keep semantic marker sets as the single writable owner, and keep viewport/selection/expanded state in view-state persistence only.

## Ordered steps

- Create an explicit owner for semantically meaningful spatial data (`X`, `Y`, ordering, future spatial semantics) separate from ephemeral viewport state.
- Unify marker ownership into one canonical marker set model; legacy marker columns become a derived compatibility surface if needed.
- Document which canvas data is canonical (position, semantic markers) and which is ephemeral (zoom, pan, selection, drag state).
- Add tests that preserve spatial semantics and markers across node edits and type transitions.

## Guardrails

- Do not move X/Y into disposable UI-only persistence if they carry project semantics.
- Do not keep marker truth duplicated across metadata and legacy columns once the new owner exists.

## Acceptance criteria

- Semantic X/Y and marker data survive node evolution and projection rebuilds.
- Only one writable owner exists for semantic markers.
- Viewport/selection state remains outside canonical node truth.
