# Target Solution

## Boundary Summary

- `CanDoItAll.Web`
  - Hosts the central HTTP API because it already owns DB access, managed files, workspace settings, and module services.
- `CanDoItAll.Modules.Workbench`
  - Continues to own project-structure graph mutation and projection.
  - Gains focused supporting services for checklist derivation, asset revision flows, and import orchestration only where the behavior truly belongs to project-structure domain logic.
- `CanDoItAll.Modules.Workspace`
  - Owns persisted agent profiles, approval settings, central base URL hints, and setup-snippet generation because those are workspace administration concerns.
- `CanDoItAll.Mcp.ProjectStructure`
  - New stdio MCP server that is a thin remote client with typed HTTP contracts, response shaping, and zero direct DB/file access.
- `CanDoItAll.Mcp.Core`
  - Reuses existing envelopes and may receive shared HTTP-client or lease-result primitives only when those are genuinely reusable across MCPs.

## Central API Shape

- `GET/POST /api/project-structure-mcp/...`
- Auth model:
- agent token identifies a persisted agent profile
- central API resolves effective capabilities and project-specific approval rules
- all mutation endpoints validate capability, approval policy, and lease ownership before touching domain services
- Core route groups:
- `projects`
  - list, create, get hierarchy, add or reconnect subprojects
- `structure`
  - get filtered surface, create node, update node, update metadata, status, priority, marker, move, reparent, delete
- `checklists`
  - get unfinished items with prerequisite and effective-priority details
- `assets`
  - get readonly asset metadata and download route
- `assets`
  - create revised asset node under an original asset node
- `knowledge`
  - search or list static best-practice guidance and mission text through a provider abstraction
- `leases`
  - acquire, renew, release, and inspect project or repo-branch reservations
- `approvals`
  - create approval-request nodes or records when policy blocks an operation
- `imports`
  - accept structured import requests and map them to project or node creation
- `analytics`
  - record MCP operation metadata for later audit

## Lease Model

- Central DB-backed lease record with:
- resource scope kind
- normalized scope key
- owning agent profile id
- owning machine label
- lease token
- acquired, renewed, and expiry timestamps
- descriptive reason
- Scope kinds:
- `project`
- `project-node`
- `repo-branch`
- Mutation endpoints require either:
- an already held valid lease token for the affected scope
- or a safe single-shot internal lease path when the operation is atomic and low-risk
- Conflict responses expose owner label, scope, reason, and expiry to help agents wait instead of guessing.

## Policy Model

- Workspace settings gain:
- central MCP base URL hint
- agent profiles with generated token
- boolean capability flags for read, mutate, import, knowledge, setup, and lease operations
- default estimate thresholds such as auto-approve-under-minutes and approval-required-over-minutes
- optional project-specific policy overrides
- Effective policy evaluation happens in the central API, not inside the MCP client.

## Checklist Model

- Build a focused query service that:
- loads project structure plus hierarchy context
- identifies unfinished nodes from status and progress rules
- derives prerequisites from parent chains and `Blocks` or `DependsOn` links
- computes effective priority by propagating child priority upward unless an ancestor is paused, stopped, or complete
- emits compact checklist DTOs suitable for MCP context reduction

## Knowledge Model

- Define `IProjectManagementKnowledgeProvider` with a default static implementation.
- Ship curated static guidance entries now, including:
- planning best practices
- reporting expectations
- approval and estimate guidance
- risk escalation guidance
- the explicit mission statement from the raw request
- Keep the contract ready for a future knowledge-db or vector-backed implementation without forcing that dependency now.

## Import Model

- Import is a dedicated orchestration surface, not a side effect inside generic create-node routes.
- First-pass support should focus on formats feasible in the repo and easy to validate.
- Every import run should:
- record source type
- create or attach original source asset when relevant
- create mapped structure nodes through existing domain services
- emit analytics and explicit warnings for unsupported source constructs

## Setup And Rollout

- Add a new published MCP binary and settings template.
- Extend reinstall and config scripts to include the new project-structure MCP.
- Generate UI-visible setup instructions from workspace settings so a workstation can copy a deterministic config snippet.
- Keep the main-machine address explicit in settings and generated output instead of hiding it in code.
