# SB04 Proof Manifest

- Subbundle: `SB04`
- Status: `Completed`
- Owned requirement: `REQ-005`
- Raw notes: close the runtime-host run-detail readback gap without adding mutating manager commands or execution-capable driver surfaces.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Bundle start SHA: `430496c5e7217a847e9172dcc0c2fba57f75f75c`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | `81619c12e31895609224b812e55b0d8fb6cbc4990d32112c3b919d7cae41706f` | `400b11e0df4f96ac4b969cdd021f9d859e66b2e059ce97342498eda5de341900` |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` | `3c115b8e54d6fb520c3d2ad411738f3ad920362990980f1e542c81ea7f0118c6` | `98cbfe44c8576d8c002df6dc5687c3cb040b21f4be5c3b90a2203053f5bb5a18` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`
- Passing integration readback transcript: `bundle://proof/SB04/transcripts/focused-integration-readback.txt`
- Passing Playwright UI transcript: `bundle://proof/SB04/transcripts/focused-playwright-runtime-host-ui.txt`
- Source assertion transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Boundary scan transcript: `bundle://proof/SB04/transcripts/boundary-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Screenshot inventory: `bundle://proof/SB04/transcripts/screenshot-inventory.txt`

## Browser Evidence

- Route: `/processes?processId={definitionId}&runId={runId}`
- Viewport: `1900x1200`
- Playwright proof: `Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback`
- Screenshots:
  - `bundle://proof/SB04/screenshots/01-selected-run-summary-large-desktop.png`
  - `bundle://proof/SB04/screenshots/02-runtime-host-readback-large-desktop.png`
  - `bundle://proof/SB04/screenshots/03-step-recovery-diagnostics-large-desktop.png`
  - `bundle://proof/SB04/screenshots/04-artifact-ledger-large-desktop.png`

## Semantic Adequacy

- Test name: `Process_runtime_host_readback_SB04_INV_001_uses_real_process_run_step_ids_and_dry_run_denial_without_mutation`
- Existing manager readback test: `Process_manager_verification_readback_SB028_INV_001_exposes_diagnostics_dto_and_audit_records`
- Existing denial projection test: `Process_manager_verification_readback_SB047_INV_001_projects_denial_category_reason_code_audit_and_no_mutation_flags`
- UI test name: `Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback`
- Invariant ID: `SB04_INV_001`
- Shallow-pass trap: a run-detail panel can show a selected run while omitting capability key, audit hash, evidence count, denial code, or no-mutation flags.
- Adversarial negative proof: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` records that baseline source lacked the SB04 real-run readback name, SB04 caller context, side-effect denial detail assertions, and strengthened UI readback assertions.
- Semantic positive proof: `bundle://proof/SB04/transcripts/focused-integration-readback.txt` exits 0 with three runtime-host readback tests passing against real process run and step ids.
- UI positive proof: `bundle://proof/SB04/transcripts/focused-playwright-runtime-host-ui.txt` exits 0 and captures the runtime-host readback panel with capability, audit hash, evidence refs, no-mutation state, and denied write lanes.
- Source assertion proof: `bundle://proof/SB04/transcripts/source-assertions.txt` verifies the SB04 readback test, caller context, denial detail assertions, required UI text assertions, and the stable `processes-runtime-host-readback` test id.
- Boundary proof: `bundle://proof/SB04/transcripts/boundary-scan.txt` verifies SB04 added lines introduce no Process Core extraction, reflection discovery, dynamic dispatch, execution-capable driver surface, mutating manager command, or representative dispatch suppression.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` reports no TODO, HACK, NotImplemented, stub, or fake-pass markers in changed SB04 files.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Manager runtime-host readback DTO | `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync` | Integration tests and process workspace readback loader | `focused-integration-readback.txt` passes `SB028`, `SB047`, and `SB04_INV_001`; source assertions verify the SB04 caller context and real run/step id test. | Baseline failing-first transcript lacks SB04 readback hardening markers. |
| Audit id/hash and evidence refs | Process manager read-only verification projection | Runtime-host readback panel | UI screenshot `02-runtime-host-readback-large-desktop.png` and Playwright transcript prove audit hash and evidence refs are visible. | UI source assertions fail if `Hash` or `evidence refs` assertions are removed. |
| No-mutation flags | Read-only manager/runtime-host contract | Integration tests and UI readback panel | Integration transcript asserts no process, transition, or finalizer mutation; Playwright asserts visible denied write lanes. | Boundary scan rejects newly added mutating manager commands or execution-capable driver surface. |
| Dry-run denial details | `ProcessDryRunExecutionHost` and readback mapper | SB04 real-run integration proof | SB04 integration test asserts `Denied`, capability key, side-effect denial category, denial code, denial message, surface count, denied surface/operation, audit ref, and read-only contract surface. | Failing-first transcript shows baseline lacked the side-effect denial detail assertion. |
| Runtime-host UI panel | `ProcessWorkspaceRunsRuntimeHostReadbackSection.razor` | Operator run-detail view | Playwright transcript and screenshot inventory prove a large-desktop run-detail panel with identity, audit, projection, host contract, diagnostics, and no-mutation flags. | Source assertions fail if the stable `processes-runtime-host-readback` test id or required text assertions disappear. |

## Closure Decision

- Entry gate: Passed because SB03 representative automation matrix and manual-contract classification completed.
- Closure gate: Passed after focused integration readback proof, focused Playwright UI proof, screenshot inventory, source assertions, boundary scan, anti-stub audit, and failing-first source proof.
- Progression decision: SB05 may proceed; runtime-host readback is operator-visible, tied to real run/step ids, and remains read-only.
