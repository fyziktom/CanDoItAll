# SB03 Semantic Invariants

## Invariant SB03_INV_001
- Invariant ID: `SB03_INV_001`
- Source raw note: RN-001 asks whether process runtime still works like before, and RN-004 requires stabilization before more runtime extraction.
- Expected behavior: The deterministic representative matrix covers Blazor automation, multi-team software delivery automation, business-plan PostgreSQL automation, runtime-host readback against real run/step ids, scheduler/workflow read-only jobs, and scheduler/workflow-origin run starts through process-owned paths.
- Disallowed shallow implementation: Reporting runtime stability from manual contract tests, skipped PostgreSQL tests, or automation methods that set `SuppressAutomationDispatch = true`.
- Failing-first test: N/A; no production behavior change in this process validation subbundle. The adversarial scan in `bundle://proof/SB03/transcripts/suppress-automation-dispatch-scan.txt` would fail if an automation proof method suppressed dispatch.
- Passing test: `bundle://proof/SB03/transcripts/focused-integration-matrix.txt` exits zero with 7/7 focused integration tests passing.
- Changed source files: no SB03 source edits. Verified source hashes are recorded in `bundle://proof/SB03/manifest.md`.
- Production assertions: `bundle://proof/SB03/transcripts/focused-integration-matrix.txt` includes the Blazor automation, software delivery automation, PostgreSQL business-plan automation, runtime-host readback, scheduler/workflow job, and process-owned trigger path tests.
- Red-team negative case: `bundle://proof/SB03/transcripts/postgresql-classification.txt` rejects treating skipped PostgreSQL coverage as a pass; `bundle://proof/SB03/transcripts/suppress-automation-dispatch-scan.txt` rejects suppressed-dispatch automation proof.
- Downstream dependency check: SB04 may proceed because deterministic runtime proof is green and the remaining live OpenAI blocker is provider/model configuration from SB02, not a deterministic process runtime blocker.
