# Target Solution

## Architecture Decision

Use `CanDoItAll.Git` as the single local git command-spec authority, and make `AgentFramework.Core` consume those specs when building workspace command plans. This is better than adding more raw `List<string>` git recipes to `WorkspaceCommandPlanBuilder` because validation, sanitization, path handling, and command grammar stay in one small project.

## Proposed Shape

- Add a command-spec builder in `CanDoItAll.Git` that can produce `GitCommandSpec` without executing it.
- Keep `GitRepositoryClient` as the execution facade by delegating to the same builder and `IGitCommandExecutor`.
- Add small strongly typed inputs where they prevent accidental option injection: branch names, revisions, path specs, and diff modes.
- Route workspace git tools through the shared specs and the existing `WorkspaceCommandProcessRunner` so receipts, sandbox boundaries, stdout/stderr limits, and timeout behavior stay consistent.
- Add read-only tools:
  - `workspace_git_status`
  - `workspace_git_diff`
  - `workspace_git_log`
  - `workspace_git_show`
- Add mutation tools:
  - `workspace_git_add`
  - `workspace_git_unstage`
  - `workspace_git_commit`
  - `workspace_git_branch_create`
  - `workspace_git_switch`

## Tool Policy

- Read-only git tools map to `ReadFiles`.
- Git mutation tools map to `ManagePaths`, require approval by default through tool policy metadata, and are classified as mutation capabilities.
- All git tools remain local and network-disabled.
- Commit messages are masked in sanitized command output.
- Tool descriptions must tell agents to inspect status/diff before staging or committing and to avoid retry loops without reading diagnostics.

## Skill And Template Structure

- Add an inline skill capability, tentatively `git-standard-operations`, in `repo://Templates/Capabilities/skills.json`.
- Add instructions at `repo://Templates/Capabilities/skills/instructions/git-standard-operations.md`.
- Assign the skill to default agents that already receive git tools. Agents without git tools should not receive the skill unless they also receive matching tools.

## Boundaries

- Do not add a second git execution path outside `WorkspaceCommandProcessRunner`.
- Do not introduce raw shell commands or command strings.
- Do not broaden process tool permissions for read-only roles.
- Do not add remote operations or destructive operations in this bundle.
