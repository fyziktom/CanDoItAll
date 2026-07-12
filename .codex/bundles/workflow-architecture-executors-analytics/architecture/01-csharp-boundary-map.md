# C# Boundary Map

| Boundary | Owns | Must not own |
|---|---|---|
| Workflows.Abstractions | Active service/runtime/launch/query contracts; typed origins and lifecycle/analytics DTOs | Runtime implementations, persistence, Blazor, provider SDKs |
| Workflows.Core | Catalog/application orchestration, launch policy, projections that depend only on contracts | Backend implementation, EF persistence, UI |
| Workflows.Runtime | Lifecycle state transitions, backend coordination, cancellation registry | UI-specific persistence or plugin discovery |
| WorkflowExecutors.Abstractions | Descriptor/contribution/executor/result/telemetry contracts | DI scanning or concrete operations |
| WorkflowExecutors.Core | Catalog, contribution registry, policy invoker, audit orchestration | File/document/spreadsheet implementations |
| Standard executor projects | Thin settings-to-operation adapters and descriptor contributions | Duplicate file/document/provider SDK logic |
| Plugin abstractions/host | Stable plugin-owned executor and manifest contracts, validation, host adaptation | References to Modules.Plugins or arbitrary UI activation |
| Common application operations | Workspace path/file/artifact/spreadsheet/image/command behavior shared across surfaces | Blazor rendering or workflow-node concerns |
| Tools.Documents | ManagedCode.MarkItDown adapter | Workflow settings or agent tool envelopes |
| UI modules | Rendering/orchestration, trusted renderer registrations, option sources | Runtime lifecycle, conversion, pricing arithmetic |
| Persistence/projection | Workflow state/usage storage and typed analytics query | Page-state calculations or event-JSON parsing in UI |
| Process workflow binding | SDK-free process contract containing selected workflow ID, optional exact version, and supported output mapping | Workflow runtime types, composite executor IDs, or arbitrary global workflow selection |
| Process workflow driver | Resolve verified typed-origin child runs, construct process-assignment launch intent/input, and map terminal workflow output | Agent selection, process persistence, direct runtime start, external-response fabrication, or service location |

## Ownership Rules

- A shared operation may return domain data and diagnostics. Runtime tools add grants/receipts; executors add node input/output, approval, and audit metadata.
- Descriptor defaults, schema, simulation, availability, and implementation association originate from one contribution.
- Plugin manifests cannot instantiate host UI components by type-name string. Trusted composition maps renderer keys to concrete component types.
- Process, scheduler, project, API, and agent adapters construct a typed launch origin and call the same launch service.
- Provider usage observations are immutable facts. Run analytics is a projection, not mutated counters scattered across UI/runtime.
- Process workflow assignments persist the selected workflow separately from generic executor display fields. Modules.Processes adapts the process contract to workflow launch/query abstractions; process core projects do not reference workflow runtime implementations.
