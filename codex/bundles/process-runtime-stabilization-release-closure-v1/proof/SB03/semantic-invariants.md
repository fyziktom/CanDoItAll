# SB03 Semantic Invariants

## Representative Automation Matrix Remains Production-Path

- Invariant ID: `SB03_INV_001`
- Source raw note: determine whether representative processes still work like before after the runtime-host/process refactor.
- Expected behavior: Blazor app delivery, multi-team software delivery, and business-plan automation complete through launch plan approval/execution, production outbox dispatch, process-mock AgentFramework execution runs, finalizer summaries, and managed artifact readback.
- Disallowed shallow implementation: using manual step transitions or suppressed automation dispatch as the representative automation proof.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt` shows `HEAD` lacked the SB03 manual-contract hardening markers.
- Passing tests: `bundle://proof/SB03/transcripts/focused-integration-matrix.txt` proves the three representative automation tests pass.
- Changed source files:
  - `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` after SHA-256 `124cb6da1d97e979d525c97effdce128b830ff7a8b9d0950c9ec7be12187a4ae`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` after SHA-256 `59fb3c1bfc2860ee6079f070dcadcc0ea4dbca92dba74e3fcdb76f7a096a0bbf`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` verifies launch plan creation/execution, production outbox drain, execution-run readback, PostgreSQL business readback, finalizer summary assertions, and manual-contract names.
- Red-team negative case: A representative test that only calls manual transitions, lacks outbox/execution readback, or leaves business PostgreSQL proof unclassified cannot satisfy the source assertions or guard.
- Downstream dependency check: SB04 and SB05 can rely on real completed representative automation behavior when validating runtime-host and scheduler/workflow readback.

## PostgreSQL Business Manual Tests Are Classified

- Invariant ID: `SB03_INV_011`
- Source raw note: close PostgreSQL business-analysis automation/readback gaps and classify old manual PostgreSQL tests correctly.
- Expected behavior: The PostgreSQL business automation proof uses `ExecuteTemplateWithProcessMockAgentsAsync`; tests that use helper-driven manual transitions are named as `manual_contract`.
- Disallowed shallow implementation: keeping manual PostgreSQL transition tests with generic names that can be counted as automation proof.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt` records that baseline `HEAD` lacked the `manual_contract` names and SB03 guard.
- Passing guard: `bundle://proof/SB03/transcripts/focused-guard-test.txt` proves the automation method uses process-mock execution/readback assertions and does not contain `SuppressAutomationDispatch = true`.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt` reports no stub markers in changed SB03 files.
- Boundary scan: `bundle://proof/SB03/transcripts/boundary-scan.txt` confirms this subbundle changed tests/guards only and did not introduce Process Core/driver/reflection/dynamic dispatch changes.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Representative process run completion | Production process runtime and process-mock automation | Blazor, software-delivery, and business-plan integration tests | Matrix transcript proves all three representative automation tests pass. | Tests fail if runs block, fail, or omit required completed/skipped step outcomes. |
| Outbox dispatch receipts | `ProcessOutboxService.ProcessPendingAsync` | `ProcessTemplateAutomationTestSupport` and integration assertions | Source assertions verify outbox drain path; support fails on dead-lettered rows. | Manual-transition tests are not accepted as representative proof. |
| Execution-run readback | Process mock AgentFramework runtime | Automation support and finalizer summary assertions | Source assertions verify `ListExecutionRunsAsync`; tests assert finalizer summaries. | Missing execution runs fail support assertions. |
| PostgreSQL persisted business readback | PostgreSQL-backed test profile and business automation proof | `AssertPersistedBusinessAutomationReadbackAsync` | Matrix transcript includes the PostgreSQL business automation test; source assertions verify persisted readback assertion. | If PostgreSQL is unavailable the test fails with explicit availability assertion rather than silently replacing proof with in-memory behavior. |
| Manual contract classification | Test method names and guard source scan | Code-first guard suite and release proof | Guard transcript proves the automation proof has no dispatch suppression; source assertions verify `manual_contract` names. | Old generic business manual test names fail source assertions. |
