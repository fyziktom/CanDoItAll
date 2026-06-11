# Stabilization target

## Runtime ownership
The Process Module remains owner of:
- template catalog/projection/import/publish,
- launch plan creation/review/approval/execution,
- process runs and step runs,
- outbox/dispatch/finalizer,
- artifacts and project/project-structure bridges,
- scheduler/workflow-origin starts,
- operator readback.

## Core boundary
`CanDoItAll.Processes.Core` remains pure/generic. It must not gain references to:
- template names/families,
- Blazor/.NET/business-analysis,
- AgentFramework/OpenAI,
- EF/storage/workspace,
- UI/Playwright,
- runtime-host or driver packages.

## Runtime-host boundary
Runtime-host remains:
- verification-only,
- dry-run-only for execution-capable planning,
- no process/transition/finalizer/claim/retry mutation,
- no command/package/network/Office/CRM/file-write execution,
- no reflection discovery/self-registration/fallback selector.

## Release goal
This bundle should decide whether `maf-processes-refactor` is stable enough to merge back toward development from the standpoint of process launching/execution.
