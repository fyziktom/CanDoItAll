# Git Standard Operations

Use this skill when local workspace git state matters to the task.

## Available Tools

- `workspace_git_status` inspects current change state.
- `workspace_git_diff` inspects unstaged content changes.
- `workspace_git_log` inspects recent history.
- `workspace_git_show` inspects one validated revision.
- `workspace_git_add` stages explicit workspace paths.
- `workspace_git_unstage` removes explicit workspace paths from the staging area.
- `workspace_git_commit` creates one local commit.
- `workspace_git_branch_create` creates one local branch.
- `workspace_git_switch` changes to one local branch.

## Workflow

1. Start with `workspace_git_status`.
2. Use `workspace_git_diff` before editing, before staging, and before committing when changes are material.
3. Use `workspace_git_log` or `workspace_git_show` only when history context changes the implementation decision.
4. Stage only explicit paths that belong to the current task.
5. Use `workspace_git_unstage` immediately when unrelated paths are staged.
6. Commit only after validation evidence exists and the staged content is one coherent change.
7. Keep branch names short, task-scoped, and readable.

## Boundaries

- Use only the tools listed in this skill.
- Do not invent git commands or shell equivalents.
- Do not stage generated noise, secrets, machine-local files, build outputs, dependency caches, or `.git` metadata.
- If a needed git operation is not available as a listed workspace tool, report the gap instead of improvising.
- If a git tool returns an error, read the tool result and fix the cause explicitly before retrying.
