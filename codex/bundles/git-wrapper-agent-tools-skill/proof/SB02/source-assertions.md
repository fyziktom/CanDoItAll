# SB02 Source Assertions

## Tool Name Coverage

| Tool | Command service | MAF plugin/composition | Policy/access | Tests |
| --- | --- | --- | --- | --- |
| `workspace_git_status` | Existing command service method now routes through SB01 specs. | Existing MAF methods remain attached. | Catalog and access metadata retain read classification. | Command and MAF tests assert presence. |
| `workspace_git_diff` | Existing command service method now routes through SB01 specs. | Existing MAF methods remain attached. | Catalog and access metadata retain read classification. | Command and MAF tests assert presence. |
| `workspace_git_log` | Added `GitLog` and `BuildGitLog`. | Added explicit and configured tool bindings. | Added catalog constant and read metadata. | Command, access, and MAF tests assert behavior. |
| `workspace_git_show` | Added `GitShow` and `BuildGitShow`. | Added explicit and configured tool bindings. | Added catalog constant and read metadata. | Command, access, and MAF tests assert behavior. |
| `workspace_git_add` | Added `GitAdd` and `BuildGitAdd`. | Added explicit and configured tool bindings with approval wrapping for configured tools. | Added catalog constant, mutation metadata, and manage-paths permission. | Command, access, and MAF tests assert behavior. |
| `workspace_git_unstage` | Added `GitUnstage` and `BuildGitUnstage`. | Added explicit and configured tool bindings with approval wrapping for configured tools. | Added catalog constant, mutation metadata, and manage-paths permission. | Command, access, and MAF tests assert behavior. |
| `workspace_git_commit` | Added `GitCommit` and `BuildGitCommit`. | Added explicit and configured tool bindings with approval wrapping for configured tools. | Added catalog constant, mutation metadata, and manage-paths permission. | Command, access, and MAF tests assert behavior. |
| `workspace_git_branch_create` | Added `GitBranchCreate` and `BuildGitBranchCreate`. | Added explicit and configured tool bindings with approval wrapping for configured tools. | Added catalog constant, mutation metadata, and manage-paths permission. | Command, access, and MAF tests assert behavior. |
| `workspace_git_switch` | Added `GitSwitch` and `BuildGitSwitch`. | Added explicit and configured tool bindings with approval wrapping for configured tools. | Added catalog constant, mutation metadata, and manage-paths permission. | Command, access, and MAF tests assert behavior. |

## Raw Note Closure

- `create with it set of tools for agents`: SB02 exposes the bounded local git tool set to agents through the workspace runtime and MAF composition.
- `standard operations with git`: status, diff, log, show, add, unstage, commit, branch create, and switch are available; remote/destructive operations remain intentionally absent.
- `new tools`: `ToolContractCatalog`, `ToolCapabilityRegistry`, `AgentWorkspaceToolAccessMetadata`, and MAF composition now recognize the final tool names.
