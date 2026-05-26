# SB10 Semantic Invariants

- Source raw note: Bundle required seeded baseline scenarios not to masquerade as live tests and required current-run artifact lineage proof.
- Invariant ID: SB10-INV-001
- Expected behavior: Required evidence and seeded baseline fixtures use generic Blazor WASM PWA names, while stale/sample-specific artifacts cannot satisfy current-run proof.
- Failing-first test: proof/SB10/transcripts/failing-first.txt
- Passing test: proof/SB10/transcripts/passing.txt
- Changed source files: repo://Templates/Processes/seed-catalog/baseline-scenarios.json; repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: The baseline is renamed generically, writeback receipt assertions are generic, and stale/current-run target tests remain passing.
- Red-team negative case: proof/SB10/transcripts/failing-first.txt proves old sample-specific baseline/writeback evidence names are absent.
- Downstream dependency check: SB11, SB12, and SB16 use this evidence boundary for project-structure writeback and closure.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect required behavior.
