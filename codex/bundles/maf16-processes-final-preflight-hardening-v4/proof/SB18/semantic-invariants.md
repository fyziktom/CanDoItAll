# SB18 Semantic Invariants

- Invariant ID: `SB18-INV-001`
- Source raw note: `RQ10` requires a final go/no-go report before full real UI process testing.
- Expected behavior: the report must pass build and focused tests where available, but return NO-GO when broad integration or live step0 evidence is incomplete.
- Disallowed shallow implementation: marking the bundle green from source edits, partial tables, or route-only browser evidence without the required full validation proof.
- Failing-first test: bundle://proof/SB18/transcripts/integration-filter-tests.txt records the broad integration filter timeout with a non-zero exit code.
- Passing test: bundle://proof/SB18/transcripts/build.txt, bundle://proof/SB18/transcripts/unit-filter-tests.txt, bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt, and bundle://proof/SB18/transcripts/component-process-tests.txt record passing slices.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs.
- Production assertions: bundle://proof/SB18/transcripts/source-assertions.txt and bundle://proof/SB18/transcripts/anti-stub-audit.txt show no stub closure and typed runtime assertions.
- Red-team negative case: bundle://proof/SB18/transcripts/integration-filter-tests.txt and bundle://proof/SB12/browser-live-processes-route.png keep the final result at NO-GO.
- Downstream dependency check: bundle://proof/SB18/transcripts/build.txt, bundle://proof/SB18/transcripts/unit-filter-tests.txt, and bundle://proof/SB18/transcripts/component-process-tests.txt cover build/unit/component downstream checks.
