# SB11 Semantic Invariants

- Invariant ID: `SB11-INV-001`
- Source raw note: `RQ06` requires the read model to consume finalizer diagnostics for all rejected statuses.
- Expected behavior: a recorded artifact with a non-satisfied finalizer diagnostic is projected as that rejected status, keeps diagnostic metadata, and is never reported as satisfied or auto-projected.
- Disallowed shallow implementation: matching only `ContentUnavailable`, ignoring latest rejected diagnostics, or hiding attempted path, owner, and suggested action metadata.
- Failing-first test: bundle://proof/SB11/transcripts/failing-first.txt demonstrates repository HEAD lacked all-status diagnostic consumption.
- Passing test: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt includes `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs.
- Production assertions: bundle://proof/SB11/transcripts/source-assertions.txt shows all rejected statuses are mapped and the diagnostic builder carries metadata.
- Red-team negative case: bundle://proof/SB11/transcripts/failing-first.txt rejects the old content-unavailable-only matching path.
- Downstream dependency check: bundle://proof/SB18/transcripts/build.txt and bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt prove read-model consumers build and pass the focused integration slice.
