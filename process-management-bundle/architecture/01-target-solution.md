# Target Solution

## Module Boundaries

- `CanDoItAll.Modules.Processes`
  Canonical owner of process definitions, process versions, roles in process context, runtime runs, step runs, work briefs, handoffs, decision records, policy outcomes, journals, conformance records, and improvement candidates tied to process evidence.
- `CanDoItAll.Modules.CrmHr`
  Canonical owner of workforce identities, suppliers, parties, reusable business role templates, staffing templates, recruiting and sourcing flows, and durable AI identities.
- `CanDoItAll.Modules.Workspace`
  Canonical owner of provider profiles, environment-level AI/provider configuration, workspace-level runtime settings, and related administration truth.
- `CanDoItAll.Modules.Projects`
  Canonical owner of project scope, project hierarchy, and delivery context.
- `CanDoItAll.Modules.Workbench`
  Projection owner only:
  useful visual surface and seed helper, but never a hidden process store.
- `CanDoItAll.AgentFramework`
  Future runtime adapter:
  execution mechanics, sessions, runtime logs, and external coordination only after they are correlated back to process truth.
- `CanDoItAll.Mcp.Processes`
  Local MCP projection owner:
  exposes process-definition and runtime access through stdio tools over canonical process services, install-time settings, and repo-local configuration only.

## Canonical Ownership Rules

| Concern | Canonical owner now | Process-module rule |
| --- | --- | --- |
| Process definition and run truth | Processes | Never duplicate inside Workbench or AgentFramework. |
| Business role templates and staffing archetypes | CRM-HR | Processes reference and snapshot them. |
| Durable AI identity | CRM-HR | Processes bind to identity snapshots, not runtime-owned agent records. |
| Provider profiles | Workspace | Processes and AgentFramework consume shared provider truth. |
| Project scope and project hierarchy | Projects | Processes link through typed references only. |
| Runtime sessions, logs, metrics | Future AgentFramework bridge + process correlations | Evidence must remain attributable to process context. |
| Artifact files and evidence payloads | Managed artifact store now, IPFS seam later | Trust state stays in process/domain metadata. |
| MCP access to process definitions and runs | Processes via `CanDoItAll.Mcp.Processes` projection | MCP must reuse process services and never become a second domain or store. |

## Process Domain Must Model Now

- `ProcessDefinition`
- `ProcessDefinitionVersion`
- `ProcessRoleRequirement`
- `ProcessRoleBindingSnapshot`
- `ProcessStepContract`
- `ProcessDecisionRecord`
- `ProcessAssignmentReason`
- `ProcessPolicyEvaluationRecord`
- `ProcessEscalationReason`
- `ProcessWorkBrief`
- `ProcessBatonHandoff`
- `ProcessArtifactSnapshot`
- `ProcessArtifactTrustState`
- `ProcessRun`
- `ProcessStepRun`
- `ProcessRuntimeEvent`
- `ProcessOperatingMode`
- `ProcessAutonomyPolicy`
- `ProcessRefusalReason`
- `ProcessExternalExecutionLink`
- `ProcessConformanceObservation`
- `ProcessImprovementCandidate`

## Enterprise Extension Points That Must Exist From The Start

- Explainability records for why a role, executor, process version, or escalation path was chosen.
- Policy and autonomy envelopes that can later constrain AI runtimes without changing canonical process truth.
- Artifact trust, lineage, review schedule, and allowed-usage policy metadata.
- Forensic replay inputs for policy version, environment snapshot, message and tool evidence, and baton chain reconstruction.
- Operating modes and refusal outcomes so the platform can safely do nothing when conditions are unsafe.
- Outcome, economics, capability-gap, and collaboration-quality analytics seams.

## Storage Strategy

- Use the shared `AppDbContext` pattern first.
- Keep dual SQLite and PostgreSQL support in lockstep from the first process tables.
- Keep high-volume journal and evidence tables append-oriented and extraction-ready.
- Keep artifact metadata in canonical process storage while actual file payloads flow through existing managed storage and later an IPFS-backed adapter.

## UI Strategy

- Process authoring and runtime surfaces must be built with BaseLib and CanvasLib primitives first.
- Canvas layout persists independently from canonical process semantics.
- Live runtime overlays remain projections onto the authored diagram.
- Large-screen compactness is the primary UI optimization target for the first implementation pass.

## MCP Strategy

- Prefer a local stdio MCP server for process access in this phase.
- Reuse `ProcessesService` and existing composition/bootstrap logic instead of creating a second remote process API.
- Keep settings local and restart-friendly:
  repo-local JSON settings, reinstall-script publication, `.vscode\mcp.json` update, and `~/.codex/config.toml` update.
- Expose compact read and mutation tools for process definitions and process runtime data, but keep canonical validation and business rules inside the process module.
