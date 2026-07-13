# SB11 Semantic Invariants

- Invariant ID: `SB11-INV-final-closure`
- Source raw note: GPTPro RC8 and RC9 required real combination tests and operator-visible diagnostics, with raw-note closure across the whole bundle.
- Expected behavior: The implementation passes focused and full unit validation, source scans show the generic boundary is clean, and template compatibility remains intact.
- Disallowed shallow implementation: Marking the bundle complete from status tables without tests, source scans, or proof artifacts.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `ProcessTemplateCompatibilityHistoryTests` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorDiagnosticDetailsBuilder.cs`
- Production assertions: Operator diagnostics expose gate id, branch, route target, criteria ids, receipt ids, and next action without generic .NET wording.
- Red-team negative case: The anti-stub audit rejects old adapter gate loop names and domain literals in generic process application/runtime projects.
- Downstream dependency check: Execution report maps every GPTPro root cause to solved or explicitly documented validation gap.
