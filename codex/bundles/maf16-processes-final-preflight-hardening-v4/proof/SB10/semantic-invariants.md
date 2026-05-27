# SB10 Semantic Invariants

- Invariant ID: `SB10-INV-001`
- Source raw note: `RQ06` requires artifact status vocabulary for every rejected finalizer result, not only `ContentUnavailable`.
- Expected behavior: the runtime read-model contract exposes rejected finalizer results as typed satisfaction and validation values for `Missing`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, `ContentUnavailable`, and `ContentHashMismatch`.
- Disallowed shallow implementation: adding UI text or a single string diagnostic while leaving rejected finalizer results collapsed to satisfied, auto-projected, or content-unavailable-only behavior.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt demonstrates repository HEAD lacked the expanded typed status contract.
- Passing test: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt includes `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs.
- Production assertions: bundle://proof/SB10/transcripts/source-assertions.txt shows the typed status contract and focused test coverage.
- Red-team negative case: bundle://proof/SB10/transcripts/failing-first.txt rejects the previous content-unavailable-only vocabulary.
- Downstream dependency check: bundle://proof/SB18/transcripts/build.txt and bundle://proof/SB18/transcripts/component-process-tests.txt prove consumers compile and process UI tests still pass.
