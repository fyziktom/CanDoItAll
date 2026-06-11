# SB07 Semantic Invariants

- Invariant ID: `SB07-INV-001`
- Source raw note: REQ-007 representative regression matrix.
- Expected behavior: Build, full unit tests, focused integration, runtime-host UI proof, project-structure UI proof, scheduler/workflow proof, and live-smoke classification are reported with separate outcomes.
- Disallowed shallow implementation: Manual contract tests or skipped live-smoke tests cannot replace representative E2E proof.
- Failing-first test: `Process_runtime_host_codefirst_SB01_INV_008_manual_contract_tests_are_not_counted_as_automation_proofs`
- Passing test: `Process_runtime_host_codefirst_SB01_INV_008_manual_contract_tests_are_not_counted_as_automation_proofs`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`, `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- Production assertions: Matrix commands cover build, unit, focused integration, UI readback, project-structure launch, scheduler/workflow, and guarded live classification.
- Red-team negative case: Guard tests reject manual contract proof as representative automation proof.
- Downstream dependency check: SB08 consumes this matrix and still blocks release on code-first ratio.
