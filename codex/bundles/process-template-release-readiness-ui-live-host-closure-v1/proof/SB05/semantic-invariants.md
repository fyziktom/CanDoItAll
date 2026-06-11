# SB05 Semantic Invariants

- Invariant ID: `SB05-INV-001`
- Source raw note: REQ-005 live OpenAI smoke classification.
- Expected behavior: Live OpenAI process smoke runs only when explicit opt-in env settings, model, timeout, token budget, and API key are available; skipped runs are not claimed as live proof.
- Disallowed shallow implementation: A skipped test or process-mock result cannot be reported as live provider proof.
- Failing-first test: The live-smoke command classifies missing env settings rather than running live provider execution.
- Passing test: `LiveProcessRunOpenAiSmokeIntegrationTests`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`
- Production assertions: Existing live-smoke test gates prevent unbounded or accidental live provider execution.
- Red-team negative case: Changed-line secret scan found no API key, password, or secret literal values.
- Downstream dependency check: SB07 and SB08 classify the result as skipped and not counted as live proof.
