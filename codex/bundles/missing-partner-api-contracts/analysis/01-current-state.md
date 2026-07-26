# Current State

## Repository State

- CanDoItAll current commit:
  `8d65ad1092a0f3bd1089a28b6fe827a7b405fd2c`; worktree was clean before this bundle.
- SharedInfo worktree was clean before this bundle.
- No applicable CanDoItAll `AGENTS.md` was found. SharedInfo root `AGENTS.md` applies to
  SharedInfo edits.
- Canonical SharedInfo standards loaded: `docs/standards/codex.md` and the
  `apply-candoitall-shared-standards` routing contract.

## Raw-Note Recheck

| Note | Current evidence | Triage |
| --- | --- | --- |
| N001 | `AgentsApi.cs` exposes only JSON `AgentImportApiRequest(string PackagePath)`. | Missing |
| N002 | agent save remains ID-based; no public external-key lookup/upsert/delete route. | Missing |
| N003 | `AgentExecutionRunStartApiRequest.StructuredOutput` still uses `AgentStructuredOutputContract`, whose runtime shape contains `.NET Type`. | Missing |
| N004 | workflow catalog route has no stable template-key route; current catalog provenance must be inspected and extended. | Missing/partial internal data |
| N005 | workflow core has `WorkflowLaunchIdempotency` and persistent store support, but `StartWorkflowRunAsync` constructs `NotRequested`. | Missing public bridge |
| N006 | Minimal API handlers predominantly return untyped `Results.Ok`; current OpenAPI tests assert paths, not all named response schemas. | Missing |
| N007 | CRM-HR already has application-scoped recruitment interviews, but they contain prose outcome/feedback only and no typed agent/run/challenge/rubric/reviewer evidence. | Partially adjacent, canonical contract missing |

## CodeAnalytics Evidence

- Snapshot: `snap-20260725222007-d4d57050`.
- Scope: Web, AgentFramework Models/Core/Persistence/Workflows Abstractions/Core,
  Modules.AgentFramework, Modules.CrmHr, and Processes.Contracts.
- Health: 9 projects, 440 documents, no blocking load errors.
- Top relevant findings: `AgentsApi.cs` 675 lines, `WorkflowsApi.cs` 806 lines,
  `CrmHrApiContracts.cs` 618 lines.
- Dependency result: no project-level cycle in the scoped graph; two pre-existing
  module/type cycles were reported inside Modules.AgentFramework.
- Diagnostics: factory DI registrations are only partially interpreted. Exact composition
  files must be read before edits.
- Baseline warning: known high-severity advisories for
  `System.Security.Cryptography.Xml 10.0.7` appear in unrelated projects.

## SharedInfo State

- Shared API snapshot still points to pinned commit `065f31e...`, 223 paths, 266
  operations, 258 schemas, SHA-256 `324A90...C2118`.
- Agents/workflows skills omit all requested routes and portable DTO guidance.
- `candoitall-api-crmhr` exists in SharedInfo but was absent from the active installed
  skill root at preparation time.
