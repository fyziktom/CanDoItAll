# SB10 Semantic Invariants

- Invariant ID: `SB10-INVARIANT-001`
- Source raw note: `RQ-011` Documentation and static guardrails must match the providerized first-party tool boundary.
- Expected behavior: Root, architecture, MAF, Processes, and process-skill docs describe `ProcessAgentRuntimeToolProvider`, `ProjectStructureAgentRuntimeToolProvider`, and `ImageGenerationAgentRuntimeToolProvider`; provider diagnostics include key/display name and optional receipt/trace ownership fields; docs explicitly state this is not a completed process-core extraction.
- Disallowed shallow implementation: Updating bundle history only, leaving live docs stale, naming removed hard-coded attach methods in live docs, weakening architecture tests, or claiming the process dispatcher has been extracted into a process-core package.
- Failing-first test: `ApiDocsSkillsParityTests.Runtime_provider_docs_describe_current_tool_ownership_and_diagnostics` fails when required live-doc assertions are absent.
- Passing test: `bundle://proof/SB10/transcripts/dotnet-test-unit-api-docs-skills-parity.txt`.
- Static guard: `bundle://proof/SB10/transcripts/dotnet-test-unit-agent-runtime-tool-provider-architecture.txt`.
- Stale reference guard: `bundle://proof/SB10/source-assertions/stale-reference-scan.txt`.
- Changed source files: `bundle://proof/SB10/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB10/source-assertions/runtime-provider-doc-source-assertions.txt`.
- Red-team negative case: A future change that adds a direct Processes/Projects/Workbench reference to MAF or restores hard-coded project-structure/image-generation attachment names fails the static architecture guard or stale scan.
- Downstream dependency check: SB11 may start because the live documentation and static guardrails now match the runtime-provider boundary established in SB04-SB09.
