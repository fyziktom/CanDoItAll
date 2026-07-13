# C# Current-State Inventory

## Gate status

- Bundle state: `REOPENED FOR ARCHITECTURE REPAIR`
- Inventory date: `2026-07-12`
- This document records the implementation found after the original SB01-SB34 closure. It is not proof that the target architecture is implemented.
- Implementation may proceed only against the boundaries in `01-csharp-boundary-map.md` and `02-csharp-dependency-direction.md`.
- Bundle completion remains blocked by the findings in this inventory and `reviews/csharp-architecture-gate.md`.

## Evidence anchors

| Repository | CodeAnalytics snapshot | Scope/result |
| --- | --- | --- |
| `C:\repositories\CanDoItAll` | `snap-20260712092446-ac8ce1a7` | Root-built scoped snapshot covering the memory, MAF, module, composition, and test projects; 1,925 types and 16,531 members; no blocking analyzer load errors. |
| `C:\repositories\CanDoItAll` | `snap-20260712092631-7f3b2a52` | Agent-generated scoped confirmation snapshot; no blocking analyzer load errors. |
| `C:\repositories\CanDoItAll.CognitiveMemory` | `snap-20260712092333-cdf936e2` | External repository snapshot; solution currently loads 21 projects because it references sibling main-repository projects; no project cycle reported. |

Generated-code duplicate-type warnings were present in the snapshots. They do not explain or waive any architectural finding below.

The CanDoItAll Components MCP was queried during the audit, but its transport was unavailable. Existing BaseLib component usage was inspected locally instead. Component-catalog validation therefore remains an explicit validation gap; it is not permission to introduce raw replacement markup or an unverified component abstraction.

## Current project and responsibility inventory

| Current owner | Current responsibility | Assessment |
| --- | --- | --- |
| `CanDoItAll.Memory.Abstractions` | Protocol envelopes, provider identities and manifests, capabilities, selection inputs/results, operations, feedback, events, and source-facing contracts. | Correct inward owner in principle. Selection contracts do not yet carry every agent restriction needed to enforce an allowlist without relying on an outer caller. |
| `CanDoItAll.Memory.Application` | Registry, provider selection, operation handling, source capture, status/cancellation, feedback/events, workers, and source snapshot contracts. | Overloaded. It references `CanDoItAll.AgentFramework.Core`, reversing the desired dependency direction for a generic memory application layer. `MemoryOperationHandler` and `MemoryProviderEventWorker` group unrelated responsibilities through partial files. |
| `CanDoItAll.Memory.Http` | HTTP transport driver, request mapping, response mapping, and registration. | Functionally separated as a project, but one driver is split into capability-grouping partials. Request factories currently manufacture empty workspace/project context instead of carrying the runtime identity. |
| `CanDoItAll.Memory.Mcp` | MCP transport driver, request mapping, response mapping, and registration. | Same structural issue as HTTP. The production composition path does not register MCP provider support. |
| `CanDoItAll.Memory.Persistence` | EF persistence, retention projections, and generic memory module DI registration. | Persistence owns application-service registration and therefore acts as an accidental composition root. `EfMemoryRetentionProjectionStore` is split by capability rather than a permitted partial-class reason. |
| `CanDoItAll.Modules.AgentFramework` | Agent memory settings codec, selection policy resolver, runtime memory tool, context contributor, workflow executor, and UI composition. | Wrong owner for reusable MAF-to-memory integration. Runtime behavior, persistence metadata, and UI are coupled to a broad Razor module. There is no typed agent-editor surface for the settings. |
| `CanDoItAll.AgentFramework.Models` | Agent editor models and other persisted/runtime model contracts. | Does not own a typed memory settings model. The editor persists raw `ConfigurationJson`; its save path handles project/process/workspace/image/voice codecs but not memory settings. |
| `CanDoItAll.AgentFramework.Maf` | Runtime capability composition and legacy workspace-memory context attachment. | Does not propagate a complete typed memory execution identity. The generic memory tool depends on ad hoc tags that production composition does not populate. Legacy workspace memory is attached independently of generic provider configuration. |
| `CanDoItAll.Modules.Memory` | Provider administration UI and profile editing. | `MemoryProviderManagementUiService` is 741 lines and editing can overwrite transport-specific extension data. Only a provider UI URL is handled explicitly; authentication/endpoint/tool-map configuration is not safely modeled. |
| `CanDoItAll.Composition` | Base host registration and module assembly discovery. | Still references and discovers `CanDoItAll.Modules.CognitiveMemory`, so base-host decoupling is not complete and two existing architecture tests fail. |
| `CanDoItAll.CognitiveMemory` repository | Native domain/application/persistence/service implementation and protocol endpoints. | The repository is not autonomous: contracts, MAF, service, and tests reference sibling main-repository projects. The HTTP service has no effective request authentication/authorization or project access enforcement and advertises operations that return failure/not-implemented responses. |

## Runtime behavior inventory

### Agent configuration and invocation

- `AgentMemoryAccessSettings` currently exposes tool/context/source flags, singular preferred/default provider identifiers, allowed provider identifiers/capabilities/source scopes, and assignments.
- There is no strongly typed invocation mode corresponding to `Disabled`, `Automatic`, or `ExplicitDirective`.
- There is no provider binding with a stable agent-facing alias such as `memory1`.
- `/mem:<alias>` is not parsed anywhere.
- Malformed settings JSON and invalid identifiers can be swallowed by the settings reader and replaced with defaults. That is a silent fallback and is prohibited.
- `RequireContextContributions` controls failure handling; it does not implement prompt-forced memory invocation.

### Provider selection and multi-provider behavior

- The registry can hold multiple provider profiles, and a single request can select a requested/preferred/default/assigned provider.
- `InMemoryMemoryProviderRegistry.SelectProvider` does not honor `FallbackBehavior` before choosing the first enabled compatible provider.
- Selection receives no complete agent allowlist, so an implicit choice can escape an outer agent restriction.
- The context contributor executes one provider query. It cannot fan out to a configured provider set or merge provider-labelled results deterministically.
- The model is not given stable aliases for configured memory providers.
- Provider profile fields such as default policy, workspace scope, and selection tags are not consistently applied in runtime selection.

### Runtime identity and policy propagation

- `MemoryAgentRuntimeToolProvider` looks for `memory.workflowId`, `memory.workflowNodeId`, `memory.processId`, and `memory.processStepId` tags.
- Production `RuntimeCapabilityComposer` does not populate those tags and supplies an empty runtime session key in the audited path. Tests fabricate values that production does not provide.
- `MemoryAgentContextContributor` does not receive equivalent workflow/process/session identity.
- HTTP and MCP request factories currently create `MemoryWorkspaceContext.None` and execution context with null project identity.
- The external native store applies an exact project filter. A correctly project-scoped memory is therefore invisible to the current remote call, while malformed project identifiers may be treated as global by the service.

### Operation ownership and unsupported flows

- Status and cancellation lookup by operation identifier do not authorize the requesting agent/session/workflow against the operation owner.
- Status can appear complete even when a fresh provider selection would be rejected.
- Event and retention workers are registered as scoped services, but no verified hosted execution path consumes them.
- Ingestion captures snapshots/jobs, but no verified production consumer completes the ingestion path.
- Feedback and source-request operations are advertised by the native provider while the HTTP service returns `501` for them; operation status reports failure rather than a real lifecycle.
- Unsupported capabilities must be removed from manifests or return a typed `Unsupported` result. They must not be represented as operational.

### External service safety

- `/memory/*` endpoints do not have effective authentication, authorization, rate limits, or request-size protection in the audited service.
- The API key sent by the main HTTP driver is not validated by the native service.
- Recall does not apply the declared `ICognitiveMemoryAccessPolicy` and can include records whose review/access/redaction state should prevent disclosure.
- Requester, agent, tenant, session, and policy context are not enforced before candidate material is returned.
- The in-memory web service and worker can use separate databases. The PostgreSQL path has no proven migration/readiness gate.

## Structural hot spots and partial-class audit

The following capability-grouping partials are prohibited and must be removed or replaced by cohesive top-level collaborators:

| Type | Files/shape | Required disposition |
| --- | --- | --- |
| `MemoryOperationHandler` | Five partial files; query, status, cancellation, feedback, events, source capture, selection, and helpers; eight constructor dependencies; 26 members. | Keep at most a thin facade and delegate to cohesive query, operation-control, feedback/event, and source-capture handlers. |
| `HttpMemoryProviderDriver` | Main, requests, and responses partial files. | Extract request factory, response mapper, and transport invoker as internal top-level types. |
| `McpMemoryProviderDriver` | Main, requests, and responses partial files. | Extract request factory, response mapper, and MCP invocation collaborator as internal top-level types. |
| `MemoryProviderEventWorker` | Main and outbox partial files. | Separate inbox processing, outbox dispatch, and loop/lease orchestration. Register a real hosted worker only when lifecycle is proven. |
| `EfMemoryRetentionProjectionStore` | Main and apply partial files. | Use a cohesive store plus top-level projection applier/query helper, or combine it into one bounded file if it is genuinely one responsibility. |
| `AgentFrameworkWorkspaceCatalogService.Memory` | Capability partial in a broad catalog service. | Move memory serialization into a typed codec/service invoked by the catalog facade. |

Permitted partials remain limited to generated code, Razor component code-behind, platform interop, and a time-boxed migration adapter recorded in this bundle. Generated regex partial methods in protocol/source safety types and Razor component code-behind are not violations.

Other measured hot spots requiring bounded extraction or explicit justification include:

- `MemoryProviderManagementUiService`: 741 lines.
- `MemorySourceSnapshotContracts`: 660 lines and broadly referenced.
- `MemoryAgentRuntimeToolProvider`: 484 lines.
- `MemoryWorkflowExecutor`: 434 lines.
- `MemoryAgentContextContributor`: 425 lines.
- `AgentMemoryAccessMetadata`: 357 lines.
- `MemoryProviderManagementUiContracts`: 353 lines.

Line count is evidence of review priority, not a rule by itself. Extraction is accepted only when it establishes a coherent owner, explicit dependency direction, and an independently testable seam.

## Baseline validation

| Command/scope | Result on 2026-07-12 | Interpretation |
| --- | --- | --- |
| `dotnet test tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --no-restore -v minimal` | 98 passed, 2 failed. | `CP001` and `CP002` fail because base composition still references/imports `CanDoItAll.Modules.CognitiveMemory`. This is a real architecture failure. |
| Focused unit tests for memory tool/context/MAF checkpoints | 45 passed. | Useful characterization only; they do not prove production identity propagation because test contexts provide tags absent from production composition. |
| `dotnet test C:\repositories\CanDoItAll.CognitiveMemory\tests\CanDoItAll.CognitiveMemory.Tests\CanDoItAll.CognitiveMemory.Tests.csproj --no-restore -v minimal` | 28 passed. | Does not cover endpoint authorization, access policy, project isolation, main-driver compatibility, invocation modes, or multi-provider agent behavior. |

The repository uses .NET SDK `10.0.204` with `global.json` selecting the `10.0.200` feature band. The focused unit test project uses xUnit v2 through VSTest. A known `NU1903` warning for `Microsoft.OpenApi 2.0.0` appeared in the unit build and remains separate tracked debt unless the repair changes that dependency.

Do not run builds that share output directories in parallel; the baseline audit reproduced file-lock interference when doing so.

## Current closure blockers

1. Typed agent memory settings, aliases, invocation mode, and `/mem:<alias>` semantics are absent.
2. Agent selection can silently fall back and cannot execute a deterministic multi-provider plan.
3. Project/workflow/process/session identity is not propagated consistently through tool, contributor, protocol, and native service.
4. Operation status/cancellation lacks requester ownership authorization.
5. Base composition still depends on the removed native module.
6. Capability-grouping partial classes and misplaced registration responsibilities remain.
7. Provider configuration editing can destroy transport extensions and does not safely model secrets.
8. The external service does not enforce authentication, authorization, access/redaction policy, or project isolation.
9. Manifests and DI registration overstate unsupported MCP/feedback/event/source/async behavior.
10. Cross-repository and real-agent end-to-end proof is absent.

