# SB048 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

Gate P is not satisfied by a single focused scenario or a report-only scan. It must prove the restored process runtime is buildable, covered by full unit tests, covered by representative integration scenarios, covered by large-desktop process-start UI proof, and still free of forbidden runtime-host, Core, driver mutation, bundle-path, and UI/media drift.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- source or tests depend on `codex/bundles/process-runtime-restoration-ui-e2e-driver-integration-v1`;
- process-driver runtime host, runtime registry, runtime selector, driver DI registration, manager command, scheduler/workflow driver hook, or execution-capable driver path appears in production source;
- Process Core references driver, module, infrastructure, workspace/storage, EF, DI, AgentFramework, or domain-specific `.NET`, Office, business-analysis, CRM, or HR terms;
- concrete driver packages call file/network/storage/DI APIs or process mutation operations;
- UI/media source files drift outside the approved large-desktop Playwright proof path;
- browser validation is replaced with small, medium, mobile, or non-process-route proof.

## Semantic Positive Proof

- `bundle://proof/SB046/transcripts/solution-build-no-restore.txt` proves the solution builds.
- `bundle://proof/SB046/transcripts/full-unit-tests-no-restore.txt` proves full unit coverage passes after stale guard maintenance.
- `bundle://proof/SB046/transcripts/focused-integration-scenario-matrix.txt` proves representative runtime scenarios still pass.
- `bundle://proof/SB046/transcripts/large-desktop-process-start-playwright.txt` and `bundle://proof/SB046/transcripts/large-desktop-playwright-artifact-inventory.txt` prove the required large-desktop `/processes` UI smoke.
- `bundle://proof/SB047/transcripts/release-candidate-source-scans.txt` proves forbidden source drift is absent.

## Anti-Stub Proof

`bundle://proof/SB048/transcripts/anti-stub-release-candidate-negative-proof.txt` proves synthetic shallow passes are rejected for bundle coupling, runtime-host drift, Core leakage, driver mutation, and UI/media drift. A green build alone, a non-empty transcript, or deleted guard coverage cannot satisfy this gate.

## Raw-Note Closure

- RN-008 is solved for bundle scope: Gate P repeats the large-desktop process-start Playwright smoke at `1900x1200` and records screenshot inventory. No small/medium/mobile coverage is claimed.
- RN-009 remains partially solved through Gate P: SB001-SB048 now have separate gate rows and release-candidate proof; documentation and final closure remain SB049-SB054.

## Production Behavior Artifact Matrix

No production runtime behavior was added. Gate P validates the accumulated behavior and updates executable proof only.

| Artifact | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Full unit test run | `bundle://proof/SB046/transcripts/full-unit-tests-no-restore.txt` | Gate P/release review | Re-run before claiming release-candidate readiness. |
| Focused integration matrix | `bundle://proof/SB046/transcripts/focused-integration-scenario-matrix.txt` | Gate P/release review | Re-run when process runtime scenarios or trigger/start paths change. |
| Large-desktop Playwright smoke | `bundle://proof/SB046/transcripts/large-desktop-process-start-playwright.txt` | Gate P/browser review | Re-run when `/processes` UI, launch plan, template import, or routing changes. |
| Release-candidate source scans | `bundle://proof/SB047/transcripts/release-candidate-source-scans.txt` | Gate P/source review | Re-run before final closure and when Core, driver, UI, or runtime-host boundaries change. |
