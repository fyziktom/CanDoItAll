# SB03 Semantic Invariants

- Source raw note: Bundle required the architecture step not to implement and QA/review not to mutate product files.
- Invariant ID: SB03-INV-001
- Expected behavior: Blazor WASM PWA template operation contracts keep product mutation in implementation or repair steps while validation, writeback, and escalation stay read-only or external-action controlled.
- Failing-first test: proof/SB03/transcripts/failing-first.txt
- Passing test: proof/SB03/transcripts/passing.txt
- Changed source files: repo://Templates/Processes/processes/blazor-app-delivery/definition.json; repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs
- Production assertions: The template keeps MutateProductTarget off contract, validation, record, and escalation steps.
- Red-team negative case: proof/SB03/transcripts/failing-first.txt proves unsafe mutation contracts are not present on read-only steps.
- Downstream dependency check: SB07, SB08, SB10, and SB16 depend on these operation boundaries for live-run safety.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect required behavior.
