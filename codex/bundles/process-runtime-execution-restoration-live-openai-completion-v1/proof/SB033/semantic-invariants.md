# SB033 Semantic Invariants

## Status
Completed.

## Invariant SB033_INV_001
- Invariant ID: `SB033_INV_001`
- Source raw note: processes must run from project paths and expose generated/managed output without driver runtime hooks.
- Expected behavior: Starting a process from a project-structure node creates a durable process run, a managed output artifact projects into project structure as a `process-run-output:` node under the run node, and the output node quick action opens the same project/process/run workspace.
- Disallowed shallow implementation: Seeded projection-only proof, process definition projection without a run, artifact-free project screenshot, or navigation that drops project/process/run identity.
- Failing-first/negative proof: `bundle://proof/SB033/red-team/shallow-projection-proof-rejected.md`
- Passing test: `Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node` passed in `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`.
- Changed source files: No production source changed in SB033. Current source/test hashes are captured in `bundle://proof/SB033/manifest.md`.
- Production assertions: `bundle://proof/SB033/transcripts/source-assertions.txt`
- Downstream dependency check: Manager diagnostics may start because project-structure run output projection and navigation are source-backed.

## Shallow-Pass Trap
A fake Gate K closure could reuse seeded project-structure screenshots or definition projection. SB033 rejects that by requiring a process start from a project node, managed output artifact recording, `process-run-output:` identity, and route preservation through the quick action.

## Semantic Positive Proof
- `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`
- `bundle://proof/SB033/transcripts/source-assertions.txt`
- `bundle://proof/SB033/screenshots/01-structure-run-output-node-large-desktop.png`
- `bundle://proof/SB033/screenshots/02-run-output-quick-actions-large-desktop.png`
- `bundle://proof/SB033/screenshots/03-run-output-process-workspace-large-desktop.png`

## Adversarial Negative Proof
- `bundle://proof/SB033/red-team/shallow-projection-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB033/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB033/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No matches for active bundle paths in `src`/Playwright source and no matches for execution-capable process runtime driver host surfaces.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Project node process start | Project-structure API | Process runtime | Returns `run-started` and persisted run id | Definition-only proof is rejected |
| Scoped managed output | Process artifact API | Project structure | Uses `output/scopes/project/{projectId}/process-runs/{runId}/...` | Unscoped artifact proof is rejected |
| Output quick action | Project-structure canvas | Process workspace | Opens route with project, process, and run ids | Generic `/processes` navigation is rejected |
