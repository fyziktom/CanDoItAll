# SB10 Semantic Invariants

- Invariant ID: SB10-INV-001
- Source raw note: F06 manual/API transition validation weakness and RQ07 unified artifact validation observability.
- Expected behavior: Manual/API step completion cannot satisfy required artifacts through lighter kind/title/trust checks; it must use finalizer-grade validation for content, lineage, producer kind, current-run binding, placeholder/gap markers, and managed evidence.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Failing-first test: `bundle://proof/SB10/transcripts/failing-first.txt` proves stale execution lineage is rejected even when kind/title/trust/content match.
- Passing test: `bundle://proof/SB10/transcripts/passing.txt` covers stale lineage, placeholder, malformed inline JSON, malformed storage-backed JSON, wrong producer mode, and direct shared-validator rejection.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`, dispatch validator/finalizer files, and integration tests listed in `bundle://proof/SB10/transcripts/changed-file-hashes.txt`.
- Production assertions: `TransitionStepAsync` calls the shared completion artifact validator with manual executor context before accepting required artifact completion.
- Red-team negative case: a stale required artifact with matching visible metadata is rejected rather than completing the step.
- Downstream dependency check: SB11 and SB12 can refactor runtime validation and health diagnostics without reopening manual/API completion parity.
- Required proof: adversarial manual-transition proof, passing manual/API and automation-validator tests, source assertions, anti-stub audit, changed-file hashes.
