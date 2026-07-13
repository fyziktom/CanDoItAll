# SB02 Semantic Invariants

## Agent Runtime Git Tool Contract

- Invariant ID: `WorkspaceCommandExecutionServiceTests`
- Source raw note: `create with it set of tools for agents` and `update to our new tools and skills structure so agents can use it`.
- Expected behavior: Workspace command execution, MAF tool composition, policy classification, and access metadata expose the bounded local git tool set backed by SB01 command specs.
- Disallowed shallow implementation: A runtime-only descriptor update, raw shell command construction, read-only mutation exposure, or uncataloged magic-string tool name would not satisfy the requirement.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-runtime-git-tools.txt` failed first with missing `WorkspaceCommandExecutionService` git methods.
- Passing test: `bundle://proof/SB02/transcripts/runtime-git-tools-focused-tests.txt` passed `WorkspaceCommandExecutionServiceTests`, `AgentWorkspaceToolAccessMetadataTests`, and `MafAgentRuntimeToolProviderCompositionTests`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`, and `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`.
- Production assertions: `bundle://proof/SB02/source-assertions.md` maps every shipped git tool name across process contracts, command execution, MAF runtime composition, policy registry, access metadata, and tests.
- Red-team negative case: Focused tests deny option-like revisions and `.git/config`, and access metadata tests keep git mutation tools out of read-only profiles.
- Downstream dependency check: SB03 capability templates use only the final SB02 tool names, and SB04 re-runs the runtime, access, MAF composition, and template tests together.
