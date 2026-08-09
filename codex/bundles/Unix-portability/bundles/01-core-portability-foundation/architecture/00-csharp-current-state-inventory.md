# C# current-state inventory

## Execution anchor

- Prepared source: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f` on `development`.
- Execution source: `a2856070e7303de077088fc7f2f7e96a5bcf0e70` on `unix-adoption`.
- Product delta between the two commits: none. The three intervening commits add only this bundle.
- SDK/host: .NET SDK `10.0.302`, Windows `10.0.26200`, `win-x64`.
- Proof tier for A00: `Standard`. A00 changes inventories and validation utilities but no product behavior.

## Solution facts

CodeAnalytics snapshot `snap-20260808192349-53bec4ab` covers 103 projects, 3,151 documents, 9,607 types, 72,831 members, 874 service registrations, and 117 AppDbContext entities. It reports 608 project-reference edges and zero project-level cycles. The reported three module cycles and ten type cycles are review inputs, not project graph cycles.

| Area | Current owner | Portability responsibility |
|---|---|---|
| Pure shared contracts | `CanDoItAll.SharedKernel` | Dependency-free logical-path values and comparer contracts only. |
| Physical filesystem and storage | `CanDoItAll.Infrastructure` | Root resolution, containment, symlink/reparse handling, atomic writes, permissions, and persisted storage endpoints. |
| Secret abstractions | `CanDoItAll.Security.Abstractions` | Provider-neutral secret references and runtime boundaries. |
| Secret implementations | `CanDoItAll.Modules.Security` | Vault selection, provider adapters, migration, key bootstrap, and redaction. |
| MAF execution | `CanDoItAll.AgentFramework.Core` and runtime projects | Generic execution primitives and receipts; no process-domain semantics. |
| Workbench | `CanDoItAll.Modules.Workbench` | Runtime-node metadata and presentation orchestration. |
| Manager | `tools/App/CanDoItAll.Manager` | Supervision and recovery of processes it owns. |
| Processes | `src/Processes` | Process-domain semantics, drivers, evidence, and recovery interpretation. |
| Composition | `CanDoItAll.Composition` and `CanDoItAll.Web` | Capability selection and host/profile wiring. |
| FileTools adapter | `CanDoItAll.FileTools.Integration*` | Product authorization and translation into FileTools contracts. |

## Partial-class inventory

The source contains 171 partial declarations across 73 type names. The largest clusters are `ProjectStructurePage` (22), `AgentFrameworkWorkspaceExecutionService` (10), `ProcessRuntimeEngine` (8), `ProcessManagerControlLoop` (7), `ProcessTemplateCompatibilityScanner` (6), and `AgentCapabilitySetupFlowService` (6).

No new partial split is approved by this bundle. Existing partial clusters remain under review for cohesion, but portability work must extract a service only when it establishes an independently testable boundary. File-only splitting, region relocation, and wrapper delegation do not satisfy the architecture gate.

## Current high-risk surfaces

- Logical path normalization differs between Infrastructure and MAF.
- Physical containment and case policy are inferred inconsistently from the current OS.
- Storage writes, control-plane JSON, and secret-vault files do not share one durable atomic-write contract.
- Unix secure-vault selection currently chooses unsupported providers; the file vault stores its wrapping key beside ciphertext.
- Development configuration persists Windows-shaped roots and a host-bound executable preference.
- Runtime command, working-directory, script, and process ownership fields are persisted as strings across MAF, Workbench, Manager, Plugins, and Processes.
- Shared Components and FileTools are consumed as packages. Temporary project references are deferred to B00 so the core C4 baseline remains attributable.

## Negative evidence limits

Snapshot diagnostics contain duplicate generated/display names such as `Program`; therefore the snapshot is authoritative for the project graph and positive symbol locations, but absence-of-type claims require direct source search as corroboration.
