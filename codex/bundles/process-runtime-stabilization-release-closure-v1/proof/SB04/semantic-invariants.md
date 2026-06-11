# SB04 Semantic Invariants

## Runtime-Host Readback Uses Real Process Run And Step Identity

- Invariant ID: `SB04_INV_001`
- Source raw note: close the explicit runtime-host readback gap from the previous bundle.
- Expected behavior: runtime-host readback uses a real process run id and completed step id from representative automation, returns read-only manager projection data, includes audit id/hash and evidence counts, and reports no process, transition, or finalizer mutation.
- Disallowed shallow implementation: manually seeded readback, fake run/step identity, omitted audit hash, omitted evidence refs, or an API-only proof when the user-visible run-detail gap remains.
- Failing-first proof: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` shows baseline source lacked the SB04 real-run test name, SB04 caller context, and strengthened denial/UI assertions.
- Passing tests:
  - `bundle://proof/SB04/transcripts/focused-integration-readback.txt`
  - `bundle://proof/SB04/transcripts/focused-playwright-runtime-host-ui.txt`
- Changed source files:
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` after SHA-256 `400b11e0df4f96ac4b969cdd021f9d859e66b2e059ce97342498eda5de341900`
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` after SHA-256 `98cbfe44c8576d8c002df6dc5687c3cb040b21f4be5c3b90a2203053f5bb5a18`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions.txt` verifies the SB04 real-run readback test, SB04 caller context, dry-run denial detail assertions, UI readback assertions, and stable test id.
- Browser proof: `bundle://proof/SB04/screenshots/02-runtime-host-readback-large-desktop.png` shows the operator readback panel with identity, audit hash, evidence refs, no-mutation flags, denied write lanes, host contract, and diagnostics at 1900x1200.
- Red-team negative case: a panel or DTO that omits capability key, audit hash, evidence count, denial code/message, or no-mutation flags cannot satisfy the source assertions, integration test, or Playwright assertions.
- Downstream dependency check: SB05 can rely on runtime-host readback remaining process-owned and read-only when scheduler/workflow lifecycle proof is validated.

## Dry-Run Denial Is Detailed And Read-Only

- Invariant ID: `SB04_INV_002`
- Source raw note: runtime-host dry-run denial details were not sufficiently closed for release.
- Expected behavior: dry-run readback reports denied decision, capability key, side-effect denial category, denial code, denial message, affected surface count, denied surfaces/operations, request identity, sandbox denial, audit reference, and read-only contract surface.
- Disallowed shallow implementation: checking only that a denial exists without proving why it was denied, what surface was blocked, or whether write lanes stayed denied.
- Passing proof: `bundle://proof/SB04/transcripts/focused-integration-readback.txt` includes `Process_runtime_host_readback_SB04_INV_001_uses_real_process_run_step_ids_and_dry_run_denial_without_mutation`.
- Source proof: `bundle://proof/SB04/transcripts/source-assertions.txt` verifies `sideEffectDenial.Code`, `sideEffectDenial.Message`, `sideEffectDenial.SurfaceCount`, and `ProcessRuntimeHostContractSurface.DryRunExecution`.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` reports no stub or fake-pass markers.
- Boundary proof: `bundle://proof/SB04/transcripts/boundary-scan.txt` confirms no new execution-capable driver or mutating manager command was introduced.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Real process run and step identity | Representative process automation fixture and process runtime | Manager read-only verification facade and SB04 integration test | Integration transcript proves readback uses real run/step ids and succeeds without mutation. | Failing-first transcript lacks SB04 real-run identity markers. |
| Audit hash and evidence refs | Read-only verification projection | Runtime-host UI panel | Playwright screenshot and source assertions prove `Hash` and `evidence refs` are rendered and asserted. | UI assertion removal fails source assertions. |
| Denied write lanes | Runtime-host read-only contract | Integration and UI tests | Integration test asserts no process/transition/finalizer mutation; Playwright asserts visible denied write lanes. | Boundary scan rejects new execution-capable or mutating surfaces. |
| Side-effect denial details | Dry-run execution host and denial mapper | SB04 readback integration test | Integration source assertions prove category, code, message, surface count, denied surface, and denied operation. | Baseline source lacked detail assertions and fails the failing-first transcript. |
| Operator-visible readback | Process workspace run-detail component | User-facing process workspace | Browser transcript and screenshot inventory prove large-desktop UI rendering of runtime-host readback. | Missing `processes-runtime-host-readback` test id or required text fails source assertions and Playwright proof. |
