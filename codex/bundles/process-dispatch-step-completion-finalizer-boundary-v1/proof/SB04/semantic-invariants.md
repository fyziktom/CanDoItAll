# SB04 Refactor Gate A Architecture Guardrails Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: Preserve original functions while continuing small dispatcher isolation.
- Expected behavior: Gate A fails on stale inventory or boundary broadening, then passes after source-backed inventory correction.
- Disallowed shallow implementation: Architecture tests that only compile or ignore Process Core and nested type drift.
- Failing-first test: bundle://proof/SB04/transcripts/gate-a-architecture-tests-rebuilt.txt
- Passing test: bundle://proof/SB04/transcripts/gate-a-architecture-tests-passing.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs; repo://codex/bundles/process-dispatch-step-completion-finalizer-boundary-v1/inventories/02-finalizer-method-classification-template.md
- Production assertions: Processes-module behavior is preserved; no Process Core project, driver pack API, or UI file change is introduced.
- Red-team negative case: bundle://proof/SB04/transcripts/anti-stub-audit.txt rejects placeholder exception/TODO implementation markers and boundary drift for this scope.
- Downstream dependency check: Execution report gate row and final red-team scan confirm downstream SBs can proceed or close without expanding the process-driver boundary.
