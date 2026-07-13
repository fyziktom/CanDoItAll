# SB08 Semantic Invariants

- Invariant ID: `SB08-INV-acceptance-matrix`
- Source raw note: GPTPro RC7 required acceptance criteria ids instead of screenshot/build/test-only shell acceptance.
- Expected behavior: Complex requirements produce `AC-*` criteria, accepted branches must cite each criterion, and simple calculator-like work remains lightweight.
- Disallowed shallow implementation: A prompt paragraph that asks for criteria without typed criteria ids or runtime enforcement.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `Enrich_adds_acceptance_criteria_matrix_for_complex_blazor_delivery_project` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessAcceptanceCriteriaModels.cs`
- Production assertions: Acceptance matrix JSON is emitted by Workbench contributors and checked by adapter completion gates.
- Red-team negative case: Accepted branch with complete browser/runtime receipts but missing criterion ids is rejected.
- Downstream dependency check: SB10 projects missing criteria diagnostics into operator-readable details.
