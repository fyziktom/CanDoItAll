# SB03 Source Assertions

## Capability Catalog

- `Templates/Capabilities/tools.json` declares `workspace-git-log`, `workspace-git-show`, `workspace-git-add`, `workspace-git-unstage`, `workspace-git-commit`, `workspace-git-branch-create`, and `workspace-git-switch`.
- `Templates/Capabilities/skills.json` declares `git-standard-operations`.
- `Templates/Capabilities/skills/instructions/git-standard-operations.md` references only the shipped workspace git tools from SB02.

## Agent Assignment Policy

- Full git tool set assigned to software-development profiles:
  - `dotnet-application-developer`
  - `blazor-application-developer`
  - `javascript-application-developer`
  - `programming-workspace-analyst`
- Read git tool set assigned to read/review profiles:
  - `dotnet-solution-architect`
  - `javascript-solution-architect`
  - `portfolio-architect`
  - `security-reviewer`
  - `research-deep-dive-analyst`

## Excluded Tool Names

`rg -n "workspace_git_(push|pull|fetch|reset|checkout|rebase|clean|merge)|\b(push|pull|fetch|reset|checkout|rebase|clean|merge)\b" Templates/Capabilities/skills/instructions/git-standard-operations.md` returned no matches.

## Raw Note Closure

- `complementary skill so they know how to use standard operations with git`: closed by `git-standard-operations`.
- `new tools and skills structure so agents can use it`: closed by template tool descriptors, inline skill descriptor, and scoped default-agent assignments.
