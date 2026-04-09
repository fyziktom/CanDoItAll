# Normalized Requirements

## Core Delivery Requirements

- `REQ-001`
  Add `CanDoItAll.Modules.Processes` as the canonical process-management module inside the main `CanDoItAll` solution.
- `REQ-002`
  Keep process roles and role requirements canonical; concrete human, supplier, plugin, or agent executors are replaceable fulfillments of those roles.
- `REQ-003`
  Preserve single-source-of-truth ownership:
  Processes own process topology and runtime truth, CRM-HR owns business role and identity truth, Workspace owns provider truth, Projects own project scope, Workbench owns projections only.
- `REQ-004`
  Keep the first merge free of compile-time dependency on `CanDoItAll.AgentFramework`.

## Architecture And Governance Requirements

- `REQ-005`
  Model process lifecycle, versioning, publication, archival, and immutable published execution snapshots.
- `REQ-006`
  Model step contracts, explicit handoffs, decision rights, approvals, escalations, variants, exceptions, and safe refusal outcomes as first-class domain concerns.
- `REQ-007`
  Add explainability-ready structures for process selection, assignment reasons, policy evaluations, escalation reasons, autonomy decisions, and major orchestration decisions.
- `REQ-008`
  Add artifact-trust and provenance extension points for snapshots, validation state, approval state, lineage, sensitivity, retention, and allowed future usage.
- `REQ-009`
  Add forensic-replay and operating-mode extension points so later audits can reconstruct process definition version, policy version, executor, artifact context, and environment mode.
- `REQ-010`
  Add graded autonomy and constitution-style rule extension points so future runtime autonomy is constrained by process governance instead of bypassing it.

## Cross-Repo And Storage Requirements

- `REQ-011`
  Converge current overlapping provider and agent concerns by planning migration to CanDoItAll-owned registries instead of allowing long-term parallel truth inside AgentFramework.
- `REQ-012`
  Use existing managed artifact storage and storage placement seams now, while reserving a typed evidence-storage adapter seam for `CanDoItAll.IPFS`.
- `REQ-013`
  Correlate future external runtime sessions, logs, metrics, approvals, and tool invocations back to `ProcessRun`, `ProcessStepRun`, and assignment context.

## UX And Validation Requirements

- `REQ-014`
  Future authoring and runtime UI must prefer shared BaseLib and CanvasLib components before raw HTML or one-off structural CSS.
- `REQ-015`
  Future UI validation must use Playwright MCP, large-screen screenshot review, and explicit compactness, clipping, layering, and space-use checks.
- `REQ-016`
  Workbench, project views, activity, validation, and test hooks must remain projections or integrations, not alternate canonical stores.

## Execution-Workflow Requirements

- `REQ-017`
  Implementation must be split into phases containing multiple related subbundles.
- `REQ-018`
  After each phase, Codex must create `post-implementation-bundle-phaseXX` using the shared template and must close its repair subbundles before starting the next phase.
- `REQ-019`
  Every post-phase repair bundle must explicitly review architecture boundaries, canonical model integrity, helper isolation, oversized classes, component usage, persistence drift, and seed-data quality.

## Seeding And Learning Requirements

- `REQ-020`
  Prepare development and testing seed packs that cover realistic process authoring, staffing, runtime, approval, escalation, exception, refusal, and conformance scenarios.
- `REQ-021`
  Add extension points for outcome metrics, capability-gap signals, decision intelligence, economics, relationship-quality analytics, and improvement backlog generation.
- `REQ-022`
  Add executive and management-ready surfaces for bottlenecks, trust problems, rework, capability gaps, cost, and process health instead of developer-only telemetry.
