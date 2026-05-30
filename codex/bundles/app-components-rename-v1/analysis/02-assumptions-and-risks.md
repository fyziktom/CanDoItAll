# Assumptions And Risks

## Assumptions

- The requested project name should be implemented as `CanDoItAll.AppComponents` to match the rest of the solution's `CanDoItAll.*` project naming.
- Existing package references whose names start with `CanDoItAll.Components.` are intentionally retained because they refer to the sibling component libraries.
- Generated artifacts under `bin`, `obj`, and project-local artifact folders are not authoritative source references for stale-reference decisions.

## Critical Path Risks

- SB01 is the only critical path. If its project identity repair is incomplete, downstream app build and component tests become untrustworthy.
- An overbroad namespace rewrite would corrupt valid package namespace imports and create noisy compile failures across modules.
- A path-only rename without assembly/root-namespace repair would leave the name collision unresolved.

## Validation Risks

- Full solution build may be slower than needed for this narrow rename, so targeted project build plus direct consumer tests are the primary proof.
- Stale-reference searches must exclude generated outputs and bundle proof history to avoid false positives.
- Browser validation is not useful for this rename unless targeted tests or build reveal rendered behavior changes.

## Reopen Triggers

- Any exact project reference to `src/CanDoItAll.Components/CanDoItAll.Components.csproj` remains in source after SB01.
- Any compiled source still imports or declares the exact facade namespace `CanDoItAll.Components` instead of `CanDoItAll.AppComponents`.
- Targeted build or component tests fail due to the rename.
- Stale-reference search indicates the sibling repository was edited or package names were renamed.
