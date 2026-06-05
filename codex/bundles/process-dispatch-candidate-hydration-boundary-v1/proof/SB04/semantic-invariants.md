# Gate A Guardrails Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: no premature Process Core or driver API, preserve behavior, service proof only.
- Expected behavior: bundle readiness contract is valid before any production dispatcher movement.
- Disallowed shallow implementation: implementation starts from invalid bundle structure, missing inputs, nonportable source references, or missing gate proof.
- Failing-first test: N/A process/non-production guard; the repaired prepared validator is the required positive gate.
- Passing test: `proof/SB04/transcripts/sb04-prepared-validator.txt`
- Changed source files: ProcessDispatchCandidateHeaderSelector.cs, ProcessDispatchCandidateHydrationLoader.cs, ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: module-local dispatch changes proceed only after the prepared bundle validator passes and the scope excludes Process Core, production driver APIs, UI changes, and mobile proof.
- Red-team negative case: N/A process/non-production guard; invalid bundle readiness would block production movement.
- Downstream dependency check: Unlocks safe header selector and hydration loader movement in SB05-SB08.
