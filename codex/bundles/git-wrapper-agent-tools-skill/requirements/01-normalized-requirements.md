# Normalized Requirements

| Requirement | Source | Acceptance |
| --- | --- | --- |
| REQ-001 | Raw prompt: "improve git wrapper" | `CanDoItAll.Git` exposes reusable strongly typed command-spec construction for status, diff, log, show, add, unstage, commit, branch create, and switch. |
| REQ-002 | Raw prompt: "we already have some basic wrapper. study it" | Existing wrapper behavior remains covered by tests and is not replaced by a parallel git abstraction. |
| REQ-003 | Raw prompt: "create with it set of tools for agents" | Workspace command execution, MAF workspace plugin, tool composition, policy catalog, and template capabilities expose the bounded git tool set. |
| REQ-004 | Raw prompt: "standard operations with git" | Exposed operations include inspect/status/diff/log/show and local workflow mutations stage/unstage/commit/branch/switch. |
| REQ-005 | Architect note: "new tools and skills structure" | `Templates/Capabilities/tools.json`, `Templates/Capabilities/skills.json`, skill instruction asset, and default agent assignments are updated consistently. |
| REQ-006 | Core principles: strongly typed, no magic strings | Runtime git tool identifiers are centralized in `ToolContractCatalog`; branch/revision/path command inputs use typed validation where appropriate. |
| REQ-007 | Security and maintainability | No remote, credential, destructive, reset, checkout, clean, rebase, or force tools are exposed. Git path specs stay inside allowed roots and reject `.git`. |
| REQ-008 | Quality bar | Focused tests prove wrapper specs, command plans, access policy, runtime tool composition, capability template materialization, and default agent assignment validity. |
