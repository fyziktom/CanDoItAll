# Compatibility report — Customer onboarding orchestration

**Process key:** `customer-onboarding`  
**Current architecture status:** Aligned

## Current-architecture coverage
- Explicit multi-dependency modeling is used where needed.
- Explicit artifact-input modeling is used where needed.
- Decision-role requirements are modeled on decision and approval steps where applicable.
- Canvas coordinates and branch coordinates are preserved in sidecars and projected envelopes.

## Sidecar-only fields
- Shared versus local resource folders and markdown sidecars remain outside the import envelope.
- Role knowledge, experience, anti-pattern, and fitness-evidence metadata stay in resource sidecars.
- Checklist, validation, and prompt narratives stay in resource sidecars rather than the current editor envelope.
- Mermaid diagrams and architecture-review execution docs remain file-based exports.

## Follow-up recommendations
- Keep the template pack as the authored source of truth and treat the import envelope as a projection.
- Run the corrective canvas-chrome subbundle before relying on toolbox chrome extensibility.
- Keep bundle validation in CI so dependency and artifact-input regressions are surfaced immediately.

