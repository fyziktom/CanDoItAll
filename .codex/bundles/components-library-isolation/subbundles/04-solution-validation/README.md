# 04-solution-validation

## Status

- `Completed`

## Objective

- Remove moved component projects and Space3D projects from the main slnx, add a dedicated Space3D slnx, and validate the final repository split.

## Covered Inputs

- REQ-008, REQ-010 and final raw-note closure.

## Prerequisites

- SB01, SB02, and SB03 completed with passing progression gates.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/Space3D`
- `bundle://reviews/01-execution-report.md`
- `bundle://traceability/01-requirement-traceability.md`
- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`

## Deliverables

- Main slnx excludes the eight moved components and all Space3D projects.
- New Space3D slnx includes the Space3D projects.
- Main solution restore/build/test proof captured.
- Raw-note closure matrix updated.

## Dependency Impact

- This is final closure. If it fails, earlier subbundles must be reopened based on the failure root cause.

## Validation Depth

- Critical final closure.

## Implementation Steps

1. Edit `CanDoItAll.slnx` to remove moved component projects and Space3D projects.
2. Add a dedicated Space3D slnx containing Space3D projects.
3. Run project-reference and moved-source audits.
4. Build components repo if any package changed.
5. Build main solution and run focused tests.
6. Attempt browser smoke if build succeeds.
7. Update execution report, proof manifests, raw-note closure, and final validators.

## Scope Exceptions

- Space3D source remains in the main repo; only main slnx membership changes.

## Do Not Do

- Do not delete Space3D source.
- Do not re-add moved component projects to the main slnx.
- Do not close the bundle with pending raw-note rows.

## Acceptance Checklist

- `CanDoItAll.slnx` excludes moved component projects and Space3D.
- `CanDoItAll.Space3D.slnx` includes Space3D projects.
- Main solution builds against local packages.
- Focused tests pass or failures are diagnosed as pre-existing/unrelated with evidence.
- Raw-note closure rows are `Solved`, `Partially solved`, or `Not solved`.

## Proof Required

- `proof/SB04/manifest.md` with changed-file hashes, slnx assertions, build/test transcripts, browser proof or blocker, final source audits, and anti-stub audit.
- `proof/SB04/semantic-invariants.md` for final build graph invariants.
- Final red-team verifier artifact checking fake-proof resistance across critical subbundles.
- Completed-stage bundle validator transcript.

## Browser Validation Logging

- Target route: `/` in `CanDoItAll.Web` when app startup is available.
- Viewports: desktop and narrow if reachable.
- Required assertions: app loads, component package CSS is served, main-specific CSS is served, and no immediate Blazor boot error appears.
- Screenshot: `proof/SB04/browser-home-smoke.png` when reachable.

## Progression Gate

- Pass only when slnx membership and package isolation audits are clean, builds/tests are recorded, and raw notes are closed honestly.

## Suggested Agent Prompt

```text
Implement SB04 only. Update solution membership, validate the final split, run build/test/browser proof or record blockers, then close raw notes and final validators.
```
