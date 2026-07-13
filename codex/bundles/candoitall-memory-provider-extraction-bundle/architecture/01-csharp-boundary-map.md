# C# Boundary Map

## Decision

The repair will preserve the generic memory protocol projects, introduce a dedicated MAF integration boundary, and reduce the Razor modules to UI/composition owners. It will not repair the implementation by creating more partial files or by moving the same god classes into new folders.

This is the target ownership map for the reopened bundle. Any deviation requires an explicit pattern/dependency record before implementation continues.

## Target main-repository boundaries

| Project/namespace owner | Owns | May depend on | Must not own or depend on |
| --- | --- | --- | --- |
| `CanDoItAll.Memory.Abstractions` | Provider IDs, aliases/value objects used by the protocol, manifests, capabilities, selection policy/result, request/response envelopes, typed workspace/execution/policy/budget context, operation/feedback/event/source protocol contracts. | BCL and narrow `Microsoft.Extensions.*.Abstractions` only where unavoidable. | MAF runtime types, EF, HTTP/MCP clients, modules, native Cognitive Memory, AppDbContext. |
| `CanDoItAll.Memory.Application` | Provider registry/catalog interfaces and implementation, one-provider operation orchestration, provider access/operation authorization contracts, application handlers, provider health/selection rules, application DI extension. | `Memory.Abstractions`. | AgentFramework Core, modules, UI, EF, HTTP/MCP details, native domain. |
| `CanDoItAll.Memory.SourceGateway.Abstractions` or `Memory.Abstractions.Sources` | Generic source snapshot, provenance, sensitivity, redaction, and source-request contracts if they cannot remain cleanly in the base abstractions project. | `Memory.Abstractions` only. | AgentFramework Core, module entities, AppDbContext, provider drivers. |
| `CanDoItAll.Memory.Http` | HTTP transport adapter, endpoint/auth-reference configuration, request factory, response mapper, timeout/cancellation translation, HTTP registration. | `Memory.Abstractions` and the narrow application driver contract if that contract is not rehomed inward. | MAF, modules, persistence, native implementation types. |
| `CanDoItAll.Memory.Mcp` | MCP transport adapter, typed tool mapping, request factory, response mapper, MCP registration. | `Memory.Abstractions`, narrow application driver contract, MCP abstractions. | MAF modules, persistence, native implementation types. |
| `CanDoItAll.Memory.Persistence` | EF models/configuration/stores, migrations, retention projections, persistence registration only. | `Memory.Application`, `Memory.Abstractions`, infrastructure/EF. | Registration of HTTP/MCP/agent integration/UI; native DB models. |
| `CanDoItAll.AgentFramework.Models.Memory` | Typed persisted/editor agent memory settings, invocation mode, provider binding/alias, strict JSON codec and intentional legacy migration. | `Memory.Abstractions`, existing model dependencies. | Operation handler, drivers, DI, runtime execution, UI. |
| `CanDoItAll.AgentFramework.Memory` | Agent memory policy resolution, directive parsing, invocation planning, deterministic multi-provider orchestration, result labelling/merge, runtime tool provider, context contributor, workflow adapter, and its DI extension. | AgentFramework Core/Tooling/Models abstractions, `Memory.Abstractions`, and narrow `Memory.Application` operation abstractions. | Razor/UI, EF, HTTP/MCP implementation, native Cognitive Memory, service location. |
| `CanDoItAll.AgentFramework.Maf` | General MAF composition and propagation of the typed runtime/context intent into registered capability providers. | Existing MAF projects and the integration abstraction needed to attach contributors/tools. | Provider selection rules, `/mem` parsing, provider-specific transport or native memory logic. |
| `CanDoItAll.Modules.AgentFramework` | Agent editor UI and module-level orchestration. | Models, `AgentFramework.Memory`, BaseLib, existing module dependencies. | Runtime memory policy implementations, protocol mapping, persistence, transport clients. |
| `CanDoItAll.Modules.Memory` | Provider profile administration UI, typed driver configuration editors, health/query/operations projections. | Memory application contracts, transport configuration contracts, BaseLib. | Provider selection algorithms, transport invocation, secret material storage, EF access. |
| `CanDoItAll.Composition` / `CanDoItAll.Web` | Explicit opt-in registration of application, persistence, configured transports, MAF integration, modules, and hosted workers. | All outward implementation projects required by the chosen deployment. | Direct native module/Qdrant requirement, implicit default provider, business policy. |

If a new `CanDoItAll.Memory.SourceGateway.Abstractions` project would only wrap a handful of contracts with no independent consumer, use a cohesive `Memory.Abstractions.Sources` namespace instead. The non-negotiable rule is removal of the `Memory.Application -> AgentFramework.Core` dependency, not creation of a project for its own sake.

## Agent integration responsibilities

### Typed settings owner

The persisted settings model must include, at minimum:

- `AgentMemoryInvocationMode`: `Disabled`, `Automatic`, or `ExplicitDirective`.
- A collection of strongly typed provider bindings: stable alias, `MemoryProviderInstanceId`, and whether the provider participates in automatic context.
- Allowed capabilities and source scopes using existing typed protocol values.
- Explicit failure behavior for automatic multi-provider calls; no Boolean whose meaning changes by call path.
- Intentional migration from the existing settings shape. Missing legacy metadata may map to the prior behavior, but malformed data must return a validation error and must not silently enable or disable memory.

`AgentEditorModel` owns the typed memory editor state. The workspace catalog invokes the memory codec in the same explicit manner as the existing project/process/workspace/image/voice codecs.

### Directive and invocation owner

`CanDoItAll.AgentFramework.Memory` owns parsing and planning:

1. Parse exact `/mem:<alias>` directives without treating arbitrary prompt text as configuration.
2. Resolve aliases case-insensitively while preserving a canonical display alias.
3. Reject unknown, duplicate-binding, disabled, or disallowed aliases with a typed error.
4. `Disabled`: perform no memory call; an explicit directive returns a typed disabled error.
5. `ExplicitDirective`: perform no memory call unless one or more directives are present.
6. `Automatic`: invoke bindings marked for automatic context; explicit directives select the explicit set for that turn.
7. Execute a bounded, deterministic provider plan and label each context segment with provider identity/alias.
8. Never ask the provider registry to guess an agent-level provider.

The generic `MemoryOperationHandler` continues to execute one provider operation at a time. Agent-level fan-out belongs in the MAF integration project because it is driven by agent settings and prompt intent.

### Runtime identity owner

MAF composition must create a typed memory execution context from the existing runtime context/intent. It includes agent, requester, session, project/workspace, workflow/run/node, process/run/step, and correlation identity where present. Tool and context-contributor paths consume the same typed mapper. Magic tags are not the primary contract.

## Application handler decomposition

`MemoryOperationHandler` remains a compatibility facade only if its public interface is already broadly consumed. Its methods delegate to top-level cohesive services:

| Collaborator | Responsibility |
| --- | --- |
| `MemoryQueryHandler` | Validate query, authorize provider access, select the explicitly constrained provider, invoke driver, persist operation state, translate typed failures. |
| `MemoryOperationControlService` | Authorize and execute status/cancellation against the recorded operation owner/provider. |
| `MemoryFeedbackHandler` | Validate, authorize, dispatch, and persist feedback. |
| `MemoryProviderEventHandler` | Event validation/idempotency/loop guard and inbox transition. |
| `MemorySourceCaptureService` | Request source snapshots through the generic source boundary and persist/queue ingestion work. |

Names may be adjusted to existing conventions, but responsibilities may not be recombined into capability partials. Each collaborator receives only the dependencies it uses.

## Transport adapter decomposition

Each transport retains one public driver and uses internal top-level collaborators:

- request factory: protocol context to transport payload;
- response mapper: transport payload/status to typed protocol result;
- invoker/client: I/O, timeout, cancellation, authentication reference resolution, and safe logging;
- registration/options validator: configuration validation at startup or first explicit use.

Driver-specific configuration must round-trip unknown extension fields. Authentication is represented by a secret/environment/credential reference, never a plaintext API key copied into UI JSON or logs.

## External repository boundaries

| External project | Target ownership |
| --- | --- |
| `CanDoItAll.CognitiveMemory.Contracts` | Native API DTO mapping or a shared Memory Protocol package. It must not depend on main-app modules. |
| `CanDoItAll.CognitiveMemory.Domain` | Memory records, review/access/redaction rules, domain policies, domain events. No ASP.NET, EF, MAF, or main-app project dependencies. |
| `CanDoItAll.CognitiveMemory.Application` | Recall/ingestion use cases, access-policy enforcement, actor/project authorization inputs, ports. Depends inward on Domain/Contracts. |
| `CanDoItAll.CognitiveMemory.Persistence` | EF stores/migrations and application port implementations. |
| `CanDoItAll.CognitiveMemory.Service` | Authenticated/authorized ASP.NET endpoints, rate/request limits, DTO mapping, health/readiness, composition. It must not reference the main HTTP driver implementation. |
| Optional MAF adapter | May reference published/shared MAF abstractions only when it implements real behavior. The currently inert registration is removed rather than hosted as proof. |

Until a shared protocol package is published, a sibling project reference to the protocol abstractions may be used as a recorded migration constraint. The external service must not depend on `CanDoItAll.Memory.Http`, a main UI module, main composition, or main persistence.

## UI boundary

Agent memory settings are rendered as a dedicated child component in the existing agent details dialog using the project's BaseLib wrappers and established form controls. The UI edits the typed model; it does not parse configuration JSON or implement provider selection.

Provider management uses driver-specific editors/codecs behind a typed configuration surface. Editing a common field must preserve unknown driver extensions. Components MCP validation must be repeated when its transport is available; until then, use only components already evidenced in the repository (`Tabs`, `FormSection`, `Grid`, `Stack`, `SurfaceCard`, `Alert`, `Button`, and established input wrappers/controls).

## Partial-class policy

Allowed:

- generated code;
- Razor component code-behind;
- platform interop required by tooling;
- a documented, time-boxed migration shim with an owner and removal checkpoint.

Forbidden:

- splitting a service by operation/capability to hide size;
- `.Helpers`, `.Requests`, `.Responses`, `.Status`, `.Apply`, or `.Outbox` partials for handwritten application/driver/store types;
- adding partials to keep a constructor or class below an arbitrary line count;
- using a partial extension as the composition root for another layer.

Folder and namespace separation is accepted only when the resulting types have explicit responsibilities and can be tested independently.

