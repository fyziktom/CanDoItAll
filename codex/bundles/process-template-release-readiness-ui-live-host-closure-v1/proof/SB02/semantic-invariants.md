# SB02 Semantic Invariants

- Invariant ID: `SB02-INV-001`
- Source raw note: REQ-002 business PostgreSQL automation reconciliation.
- Expected behavior: Business-plan process proof uses PostgreSQL-backed process-owned launch, dispatch, finalizer, execution-run readback, and artifact readback.
- Disallowed shallow implementation: A manual transition or `SuppressAutomationDispatch = true` test cannot be counted as representative automation proof.
- Failing-first test: `Process_runtime_host_codefirst_SB01_INV_008_manual_contract_tests_are_not_counted_as_automation_proofs`
- Passing test: `Business_plan_process_SB05_INV_001_completes_on_postgresql_through_automation_dispatch_finalizer_and_readback`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`
- Production assertions: Existing process service, dispatch, finalizer, execution-run, and artifact readback behavior is exercised through integration tests.
- Red-team negative case: Manual contract tests with suppressed dispatch are not accepted as representative automation proofs.
- Downstream dependency check: SB03 through SB08 consume the classification as PostgreSQL process-mock automation proof, not live provider proof.
