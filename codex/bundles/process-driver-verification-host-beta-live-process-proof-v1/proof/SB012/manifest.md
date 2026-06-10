# SB012 Gate D Proof Manifest

## Status
Passed.

## Gate Scope
- P04 deterministic runtime safety net.
- Re-runs the deterministic .NET software-delivery baseline scenario and the deterministic business-analysis process scenario.
- No production or test source changes were required for SB010-SB012.

## Changed File Hashes
| Artifact | SHA256 |
| --- | --- |
| tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs | 599540f916a2499569e791cb1b1f1a93ad6de395ac1a1470b681e768614c9ab9 |
| tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs | eb501c915fe8848e4e68674d677c8d4a41fadb97bce9e4189a0aa285336bd612 |
| src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.cs | 83ba617083157d489fd009df391889213bae4909f7052c80bf72fc4193964b71 |
| src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.cs | 2178b01dc89698ff95a30397c22838568df0a5a8c6ad849827e0e98b5c4a56eb |
| src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs | 99490d466b143bf4aff53de146bfe2bba53b7ef08f1c46251d5ea0780a8d3289 |
| Templates/Processes/seed-catalog/baseline-scenarios.json | ff16c07fb55a7d267239b5fb0df8432a969d6947609c34963fd36793f5e66e4f |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB010/transcripts/dotnet-deterministic-baseline-scenario.txt | c6500fad5c5bef6b87b62948693005405eadb634d08ff2f323e03cd640caccce |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB011/transcripts/business-analysis-deterministic-scenario.txt | 91e1276f22f4b6a3aff2b3979c93b16a24606a971e16c6c0cc541592f99244e2 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB012/transcripts/gate-d-source-assertions-and-anti-stub-audit.txt | 8e4dbfdfc5c8b59e0ffafa7a4dff8a760b1727d21702db5f9ab14e11e2fd815b |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB012/transcripts/red-team-deterministic-safety-net-shallow-proof-rejection.txt | eca8e4e1af1bf534f45c7ec6366342c76b0e5cc50044f54addc57479a7d1dc97 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB010/README.md | 945b0190e01430d9b2ff0e5014ead94f7a3407eb75a6924caa476c7516b661ec |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB011/README.md | c8abc5017e178b02654f58da68cfbe1e4898b9c350b26f83eb0668f9ba1583df |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB012/README.md | 9239280261d635d468f4df1bf08c15358e31c2989bc830736d6b5556978641f0 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/reviews/01-execution-report.md | 7bbe639674a5c7a0a7e678fd097fa0071be333096d396a4a80401058911dbbfc |

## Production Behavior Artifact Matrix
| Artifact | Classification | Gate D conclusion |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.cs` | Baseline seed entry point | `SeedBaselineAsync` imports/publishes process definitions, starts runs through `ProcessesService.StartRunAsync`, and delegates runtime state materialization to scenario seed logic. |
| `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.cs` | Deterministic runtime seed orchestration | Resolves runtime bindings, assignments, artifacts, branch outcomes, and step transitions from typed scenario data. |
| `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs` | Deterministic artifact/transition mechanics | Writes managed seed artifacts and applies only valid process step transition sequences. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | .NET deterministic scenario proof | Re-runs project-scoped software-delivery baselines and asserts the seeded run, QA accepted branch, blocked security step, artifacts, conformance observations, and release approval inputs. |
| `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` | Business-analysis deterministic scenario proof | Re-runs a non-software business-plan process, suppresses automation dispatch, records business artifacts, verifies non-software vocabulary, and asserts completion/skip status. |
| Live OpenAI paths | Out of Gate D scope | Deterministic tests do not reference live OpenAI flags or `OPENAI_API_KEY`. |

## Proof Artifacts
- SB010 .NET deterministic focused test: `bundle://proof/SB010/transcripts/dotnet-deterministic-baseline-scenario.txt`.
- SB011 business-analysis deterministic focused test: `bundle://proof/SB011/transcripts/business-analysis-deterministic-scenario.txt`.
- SB012 source assertions and anti-stub audit: `bundle://proof/SB012/transcripts/gate-d-source-assertions-and-anti-stub-audit.txt`.
- SB012 red-team rejection: `bundle://proof/SB012/transcripts/red-team-deterministic-safety-net-shallow-proof-rejection.txt`.

## Gate D Result
Passed. The deterministic runtime safety net is backed by focused integration tests for the .NET software-delivery seed path and the business-analysis process path, without depending on live-provider state or weakening process no-mutation and Core-genericity boundaries.
