# Current State

## Existing Git Wrapper

- `repo://src/CanDoItAll.Git/GitCommandContracts.cs` defines `GitCommandArgument`, `GitCommandSpec`, `GitCommandResult`, `IGitCommandExecutor`, and `GitCommandLogSanitizer`.
- `repo://src/CanDoItAll.Git/GitRepositoryClient.cs` already exposes status, diff, add, commit, branch, switch, merge, abort merge, list conflicts, log, and show.
- `repo://src/CanDoItAll.Git/GitRepositoryPath.cs` already has `GitRepositoryPath`, `GitBranchName`, `GitPathSpec`, and `GitPathAuthorizer`.
- Current wrapper issue: command construction is embedded in `GitRepositoryClient`, so callers that need command plans but not immediate execution cannot reuse the typed specs cleanly.
- Current wrapper issue: `GitPathAuthorizer` blocks `.git/...` but does not explicitly block `.git` itself or case variants.
- Current wrapper issue: branch and revision values are only non-empty strings; they should reject option-like values before they become git arguments.

## Existing Agent Runtime Git Tools

- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs` exposes `workspace_git_status` and `workspace_git_diff` recipes through raw argument lists.
- `workspace_git_status` currently builds `git status --short` plus optional `--branch`.
- `workspace_git_diff` currently builds `git diff --stat`, `git diff --name-only`, or `git diff -- <path>`.
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs` exposes only `GitStatus` and `GitDiff`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` only exposes `GitWorkspaceStatus` and `GitWorkspaceDiff`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` and `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs` attach only the two read-only git tools.

## Existing Tool And Skill Catalog

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs` has constants for `workspace_git_status` and `workspace_git_diff`.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs` classifies both as read-only workspace tools.
- `repo://src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentWorkspaceToolAccessModels.cs` maps both to `ReadFiles`.
- `repo://Templates/Capabilities/tools.json` declares only `workspace-git-status` and `workspace-git-diff`.
- `repo://Templates/Capabilities/skills.json` has repository-oriented skills, but none specifically teaches agents how to use the bounded git tool set.
- Several default agents already receive git status/diff in `repo://Templates/Agents/**/skills.json`, including .NET developers, JavaScript developers, solution architects, portfolio architect, programming workspace analyst, security reviewer, and research analyst.

## Current Tests

- `repo://tests/CanDoItAll.Tests.Unit/ProcessTemplateGitFoundationTests.cs` covers path authorization, commit-message sanitization, and add path ordering.
- `repo://tests/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs` exercises workspace command plan execution but has no focused git mutation coverage.
- `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs` covers tool-family permission mapping.
- `repo://tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs` has a hardcoded expected capability list that must be updated when adding tool and skill templates.
