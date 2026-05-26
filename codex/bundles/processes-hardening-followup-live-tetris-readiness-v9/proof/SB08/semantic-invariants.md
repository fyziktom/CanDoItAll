# SB08 Semantic Invariants

- Source raw note: Bundle required agents to have the needed Processes API, Blazor, browser, project-structure, and artifact/lineage tools instead of improvising.
- Invariant ID: SB08-INV-001
- Expected behavior: Agent dispatch and tool readiness remain governed so project-structure and external-target access do not silently fall back to unsafe behavior.
- Failing-first test: proof/SB08/transcripts/failing-first.txt
- Passing test: proof/SB08/transcripts/passing.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs; repo://src/CanDoItAll.Modules.Processes
- Production assertions: Dispatch metadata and project-structure grounding tests prove governed writable/read-only alias behavior.
- Red-team negative case: proof/SB08/transcripts/failing-first.txt proves no unsafe missing-tool fallback marker is present.
- Downstream dependency check: SB10 and SB16 depend on governed dispatch metadata before live execution proof can be trusted.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect required behavior.
