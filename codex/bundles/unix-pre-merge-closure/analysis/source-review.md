# Source review map

## Reviewed candidate

`af9206caf3c09dc25088e388727fda0e1b404833`

## High-confidence completed areas

| Area | Result |
|---|---|
| FileTools source/package provenance | Explicit source-mode opt-in and clean exact sibling anchors |
| Process ownership model | Windows Job Object and Unix process group |
| MCP framing | Bounded persistent reader, peer ping handling, unmatched-message budget |
| Docker recipes | Strict typed bool/int parsing and bounded argument collections |
| MAF baseline | 1.17 exact package constants and reflection tests |
| Approval continuation | Per-request durable decisions and resolved-request filtering |
| Agent source authority | Explicit provider for the generic agents source |
| Runtime test runner | Source fingerprint, assembly hashes, catalog and build stamp |
| Docker application stack | Non-root, read-only root, app + private PostgreSQL |

## Reviewed blockers

| ID | Source |
|---|---|
| F-001 | `ProcessInstancePlanPersistenceMapper.cs`; `AddProcessPlanHashVersioning` migration |
| F-002 | `LocalWorkspaceProcessHost.cs`; `LocalWorkspaceProcessOwnership.cs` |
| F-003 | `ManagerProcessOwnership.cs`; `WorkspaceProcessContracts.cs` |

## Evidence limitation

The retained M08 integrated evidence is valuable but its recorded commit anchor
precedes the final committed source and the subsequent MAF 1.17/authority
changes. The current reviewed commit has no hosted GitHub check run. Final
closure therefore needs a bounded exact-head rerun, not another repeated broad
test campaign.
