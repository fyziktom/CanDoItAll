# SB02 Semantic Invariants

- Source raw note: User required all reusable Blazor process/template instructions to stay generic and not demo-topic-specific.
- Invariant ID: SB02-INV-001
- Expected behavior: A generic Blazor WASM PWA live-run profile exists separately from seeded baseline scenarios, and the concrete app topic is supplied by the run request.
- Failing-first test: proof/SB02/transcripts/failing-first.txt
- Passing test: proof/SB02/transcripts/passing.txt
- Changed source files: repo://Templates/Processes/seed-catalog/live-run-profiles.json; repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackModels.cs; repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs; repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs; repo://src/CanDoItAll.Web/Api/ProcessesApi.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs
- Production assertions: The typed pack loads LiveRunProfiles, the manifest points to seed-catalog/live-run-profiles.json, and the API exposes api/processes/templates/live-run-profiles.
- Red-team negative case: proof/SB02/transcripts/failing-first.txt proves prohibited demo-topic terms are absent from process/template/runtime surfaces.
- Downstream dependency check: SB03, SB07, SB10, and SB16 consume the generic profile without seeded transition/artifact proof.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect required behavior.
