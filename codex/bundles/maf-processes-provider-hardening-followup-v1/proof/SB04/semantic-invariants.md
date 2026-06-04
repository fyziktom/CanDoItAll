# SB04 Semantic Invariants

- Invariant ID: `SB04-INVARIANT-001`
- Source raw note: `RQ-005` Project-structure internal tool attachment must move out of MAF into the owning module provider without name, access, or approval-policy drift.
- Expected behavior: Workbench registers `ProjectStructureAgentRuntimeToolProvider`; MAF attaches project-structure tools only through registered runtime providers; all pre-migration `project_structure_*` tool names remain available; access checks still read `AgentProjectStructureAccessMetadata`; approval classification still comes from `AgentToolInvocationPolicyMetadata`.
- Disallowed shallow implementation: Leaving `AttachInternalProjectStructureToolsAsync` or `CreateProjectStructureToolBuilder` in MAF, dropping or renaming any project-structure tool, bypassing provider metadata, or weakening mutation approval wrapping.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-runtime-provider-di-validation.txt` records the initial `RuntimeToolProviderComposition` integration failure when the new provider depended on unregistered `IWorkspaceCommandExecutionService`; this prevented a hidden fallback and forced explicit provider construction parity.
- Passing test: `bundle://proof/SB04/transcripts/project-structure-unit-tests.txt`, `bundle://proof/SB04/transcripts/runtime-tool-provider-composition-integration-tests.txt`, and `bundle://proof/SB04/transcripts/solution-build.txt`.
- Changed source files: `bundle://proof/SB04/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB04/source-assertions/project-structure-provider-source-assertions.txt`.
- Red-team negative case: The before/after inventory and runtime-provider composition tests would fail a tool rename/drop; the dependency scan would fail retained project-structure attach code in MAF.
- Downstream dependency check: SB05 may start with project-structure providerization complete; MAF still has a Workbench dependency only through image-generation asset storage, recorded for the SB05 image provider extraction.
