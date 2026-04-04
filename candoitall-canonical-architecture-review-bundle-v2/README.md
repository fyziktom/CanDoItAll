
# CanDoItAll canonical architecture review bundle v2

This bundle updates the previous `candoitall-canonical-architecture-review-bundle` with a **fresh review of the CRM/HR wave** and a **rethought target architecture** for the canonical model.

## Scope

Reviewed snapshot:

- repository: `CanDoItAll-crm-hr-module`
- review date: `2026-04-04`
- baseline comparison: `CanDoItAll-canvas-drawing-refactor` + the earlier canonical review bundle
- skill lenses applied:
  - `canonical-model-review`
  - `feature-block-architecture-review`
  - `architecture-drift-audit`

## What changed in this revision

The CRM/HR wave adds real party identity, staffing, AI-agent and partner structures, project/node assignments, and cross-module ownership flows.

That makes the previous canonical-model concerns **more urgent**, especially around:

- duplicated responsibility truth
- node-scoped assignment integrity
- note → task / decision evolution
- the question of whether node is a view or a stable carrier

## Updated architectural stance

The correct direction is **not**:

- “node is only a view”

The correct direction is:

- **node remains the stable universal carrier for workbench-authored project thinking**
- **typed behavior moves into explicit facets and policies**
- **module-native aggregates remain canonical in their own modules**
- **the assembled graph becomes the read model**
- **X/Y and semantic markers remain canonical**
- **viewport state remains UI-only**
- **actor/party assignments get one canonical owner per scope**

## Key numbers

- findings: **15**
- severities: **{'Critical': 3, 'High': 10, 'Medium': 2}**
- phases: **{'Phase 2': 3, 'Phase 3': 4, 'Phase 1': 3, 'Phase 0': 3, 'Phase 4': 2}**
- repo growth vs previous snapshot:
  - csproj: **+1**
  - C#: **+60**
  - Razor: **+33**
  - suspicious `manager` markers: **+19**

## How to use the bundle

1. Read `analysis/01-canonical-model-review.md`
2. Read `architecture/01-target-stabilization-architecture.md`
3. Follow `plan/01-phase-plan.md`
4. Execute `subbundles/` in phase order
5. Run the validation plan in a real .NET environment
6. Re-run the skill lenses for back-check
7. Finish with `reviews/05-final-signoff.md`

## Important limitation

This environment did **not** have `dotnet` installed, so build/test/runtime execution is still marked as **implementation-time validation** for Codex rather than something already executed here.
