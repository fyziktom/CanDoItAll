# SB09 Semantic Invariants

## Invariants

- Invariant ID: SB09-INV-001
- Source raw note: RN09 - Update template pack and live-run profiles after real-run learning.
- Expected behavior: Live-run profiles carry typed fresh-run policy that requires current-run request input, rejects seeded transitions and artifacts, and requires current-run evidence checks before validation or project-structure writeback.
- Disallowed shallow implementation: Prompt-only wording, docs-only governance, untyped profile text without model coverage, baseline scenario state imported into live-run profiles, stale validation commands, source-only proof for runtime behavior, or hardcoded Blazor/Tetris/project/run/user paths in production code.
- Failing-first test: bundle://proof/SB09/transcripts/failing-first.txt proves live-run profiles do not define `Transitions` or `Artifacts` seed collections and that the README no longer references the missing template-pack validator script.
- Passing test: bundle://proof/SB09/transcripts/passing.txt proves all 10 `ProcessTemplateGovernanceTests` pass.
- Changed source files: repo://Templates/Processes/README.md; repo://Templates/Processes/manifest.json; repo://Templates/Processes/seed-catalog/live-run-profiles.json; repo://Templates/Processes/seed-catalog/baseline-scenarios.json; repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs.
- Production assertions: `ProcessTemplatePackLoader` loads the manifest live-run profile path into typed `ProcessTemplateLiveRunProfile` records with `FreshRunPolicy`; the Blazor live-run profile declares `RequiresFreshRun=true`, `AllowsSeededTransitions=false`, `AllowsSeededArtifacts=false`, current-run evidence checks, and current-run writeback guidance.
- Red-team negative case: A live profile cannot pass governance if it grows seeded `Transitions` or `Artifacts` collections, uses demo-topic wording, or omits current-run evidence checks.
- Downstream dependency check: SB14 can build generic/non-software scenarios without reusing seeded state as live evidence, and SB17 can rely on current template docs/profile governance.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Typed fresh-run policy | repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs `ProcessTemplateLiveRunFreshRunPolicy`; source proof bundle://proof/SB09/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs and process template consumers | bundle://proof/SB09/transcripts/passing.txt proves typed policy is deserialized and asserted through the live-run profile test | bundle://proof/SB09/transcripts/failing-first.txt proves seeded transition/artifact collections are absent |
| Live-run profile seed guidance | repo://Templates/Processes/seed-catalog/live-run-profiles.json and repo://Templates/Processes/README.md | Fresh process launch guidance, SB14 scenarios, and SB17 docs parity | bundle://proof/SB09/transcripts/source-assertions.txt proves current-run evidence, writeback guidance, and no seeded state are documented and seeded | bundle://proof/SB09/transcripts/passing.txt proves demo-topic terms and seeded state do not satisfy the profile test |
| Baseline contract exercise alignment | repo://Templates/Processes/seed-catalog/baseline-scenarios.json | Template governance tests and proof harnesses | bundle://proof/SB09/transcripts/passing.txt proves baseline scenarios align with declared typed operation contracts | bundle://proof/SB09/transcripts/anti-stub-audit.txt proves the correction is not placeholder or pending text |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB09/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB09/transcripts/passing.txt.
- Source assertions: bundle://proof/SB09/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB09/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB09/transcripts/changed-file-hashes.txt.
