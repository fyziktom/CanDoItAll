# C# Pattern Selection Records

## Record status

These records constrain the reopened repair. A pattern is selected only where its forces justify the indirection. New interfaces require either multiple implementations, a test seam at a real boundary, or isolation of external I/O/policy.

## PSR-01 - Provider driver strategy

- Decision: **retain and tighten** the strategy pattern around the existing typed memory provider driver contract.
- Forces: HTTP, MCP, mock, and future external transports implement the same protocol capabilities but have different I/O/configuration behavior.
- Dependency effect: application code depends on the driver port; transport projects implement it outward.
- Testability: contract tests run the same capability matrix against each driver.
- Cost control: one driver interface and typed capability results; no interface per transport operation unless lifecycle/capability differences require it.
- Rejected alternative: switch statements over provider kind in `MemoryOperationHandler`. That would make the application layer depend on transport details.

## PSR-02 - Provider catalog plus constrained selection policy

- Decision: **retain a catalog/registry**, but separate catalog lookup from policy selection and carry allowed provider IDs into the selection input.
- Forces: multiple registered provider profiles, capability compatibility, workspace assignments, explicit agent binding, health/enabled state, and fallback policy.
- Dependency effect: registry and selector remain in Memory Application; agent-specific planning remains outside in AgentFramework Memory.
- Testability: selector tests use immutable catalog fixtures and assert exact rejection reasons.
- Cost control: do not introduce a general service locator or plugin framework. Catalog entries contain typed provider identity/manifest and a driver reference/factory defined by the memory boundary.
- Rejected alternative: "first compatible enabled provider." It violates explicit `Deny` fallback and makes registry order observable business policy.

## PSR-03 - Agent invocation planner

- Decision: introduce a small **policy/strategy service** that converts typed agent settings plus parsed prompt directives into an immutable invocation plan.
- Forces: three modes, explicit alias overrides, multiple automatic providers, allowlists, deterministic ordering, and different explicit/automatic failure rules.
- Dependency effect: lives in `CanDoItAll.AgentFramework.Memory`; depends on typed settings and memory identities, not transports.
- Testability: pure table-driven tests cover all modes and alias combinations with no DI container or network.
- Cost control: one planner, one result union/error model. Do not add one class per invocation mode unless behavior grows enough to justify independent strategies.
- Rejected alternative: Boolean checks distributed through tool provider, context contributor, and UI. They would drift and make mode behavior path-dependent.

## PSR-04 - Memory directive parser and provider alias value

- Decision: introduce a **pure parser** and a validated provider-alias value object for exact `/mem:<alias>` directives.
- Forces: prompts are untrusted text, aliases must be stable and unambiguous, and directives must be removable/retained according to one explicit rule before the provider query is formed.
- Dependency effect: parser is transport-independent and remains in AgentFramework Memory; the alias type may live in Models or Memory Abstractions if persisted/shared.
- Testability: deterministic parser tests include whitespace, casing, multiple directives, duplicates, malformed syntax, and false positives embedded in prose/code.
- Cost control: no command framework or regular-expression partial class unless an existing command parser can be extended without coupling memory to UI.
- Rejected alternative: `string.Contains("/mem:")` and string-key dictionaries. They are ambiguous and stringly typed.

## PSR-05 - Agent multi-provider orchestrator

- Decision: introduce an **orchestrator** that executes a bounded immutable plan through the one-provider operation handler and merges provider-labelled context in plan order.
- Forces: one agent may use several memory providers; provider latency/failure must not make merge order nondeterministic; explicit and automatic requests have different failure expectations.
- Dependency effect: orchestration is AgentFramework-specific and does not move into the generic registry.
- Testability: inject a narrow operation client delegate/interface and a clock only if required; assert call order/inputs, bounded concurrency, cancellation, diagnostics, and merged output.
- Cost control: no generic workflow engine. A small orchestrator with a typed result is sufficient.
- Failure rule: explicitly requested providers fail the explicit memory request when any requested provider is rejected/unavailable. Automatic best-effort is allowed only when settings explicitly select it; failures remain visible as typed diagnostics and logs, never silently omitted.
- Rejected alternative: have every context contributor select one provider independently. It cannot implement coherent multi-provider semantics.

## PSR-06 - Runtime context mapper

- Decision: use a **typed adapter/mapper** from MAF runtime/context intent to `MemoryWorkspaceContext`, `MemoryExecutionContext`, requester/policy context, and operation owner.
- Forces: tool and context paths currently construct different/incomplete identity and depend on magic tags.
- Dependency effect: adapter lives at the AgentFramework Memory boundary; protocol types remain unaware of MAF.
- Testability: mapper tests use real MAF context records and assert every available identity field and null/absence semantics.
- Cost control: one mapper shared by tool, contributor, and workflow paths. No bidirectional auto-mapper package.
- Rejected alternative: continue adding `memory.*` tags. Tags may remain compatibility input during migration but cannot be the primary typed contract.

## PSR-07 - Application facade with cohesive handlers

- Decision: preserve `IMemoryOperationHandler` as a **facade for compatibility**, delegating to cohesive query, operation-control, feedback/event, and source-capture collaborators.
- Forces: broad existing call surface, eight dependencies, 26 members, unrelated operation lifecycles, and prohibited capability partials.
- Dependency effect: collaborators stay in Memory Application and depend on narrow ports.
- Testability: each handler has direct isolated tests; facade delegation has a small smoke test.
- Cost control: reuse a shared validation/authorization collaborator only where behavior is genuinely identical. Do not create one-line wrapper interfaces for every method.
- Rejected alternative: merge partial files into one 1,000-line class. That removes `partial` without fixing ownership.

## PSR-08 - Transport request factory, response mapper, and invoker

- Decision: use three internal top-level collaborators per transport where responsibilities are independently non-trivial.
- Forces: context mapping, response/status mapping, I/O/auth/timeout, and configuration validation change for different reasons.
- Dependency effect: all remain inside their transport project and point to protocol/application ports.
- Testability: request and response mapping are pure tests; invoker uses a fake `HttpMessageHandler` or MCP client port.
- Cost control: merge a collaborator back into the driver if it remains trivial after extraction. The target is cohesion, not a file count.
- Rejected alternative: `.Requests`/`.Responses` partial files. File separation is not responsibility separation.

## PSR-09 - Operation access policy

- Decision: introduce an explicit **authorization policy/service** for operation status, cancellation, feedback, and event actions.
- Forces: operation identifiers are bearer-like and current lookup does not prove requester/agent/session/workflow ownership.
- Dependency effect: application handler depends on a typed policy; transport/UI callers supply identity context but do not decide authorization.
- Testability: direct positive/negative ownership matrix tests; no ASP.NET server required for domain/application policy tests.
- Cost control: one policy boundary with a default fail-closed implementation. Avoid duplicating authorization comparisons in every handler.
- Rejected alternative: trust an unguessable GUID. Identifier entropy is not authorization.

## PSR-10 - Driver configuration codec/editor

- Decision: use a **typed configuration codec** per driver behind one provider-configuration boundary, preserving unknown extension fields.
- Forces: HTTP/MCP require different endpoint/tool/auth fields; common profile editing currently strips extensions; secrets need references rather than raw values.
- Dependency effect: transport projects own option validation/serialization contracts; module UI consumes safe editor models without invoking transport.
- Testability: round-trip tests preserve unknown extensions and prove sensitive values never appear in UI models/logs.
- Cost control: no reflection-driven settings framework. Register codecs explicitly by typed driver kind.
- Rejected alternative: a single `Dictionary<string, JsonElement>` edited directly by Razor. It is stringly typed and destructive.

## PSR-11 - Hosted inbox/outbox processing

- Decision: use a **hosted worker plus application processors** only for features that are actually enabled and backed by durable stores.
- Forces: event polling, ingestion, and outbox dispatch require lifecycle, leases, retries, idempotency, and cancellation.
- Dependency effect: host owns `IHostedService`; application owns processors/ports; persistence implements durable stores.
- Testability: processor unit tests plus host lifecycle integration tests with deterministic polling controls.
- Cost control: unsupported event/ingestion capabilities are removed from manifests until the full path exists. Registration of an unused scoped worker is deleted rather than decorated.
- Rejected alternative: background `Task.Run`, hidden timers, or advertising a capability because contracts exist.

## Explicitly rejected patterns

| Pattern/approach | Reason rejected |
| --- | --- |
| Handwritten capability partial classes | Conceals coupled responsibilities and constructors; does not create test seams. |
| Service locator or named `IServiceProvider` lookup | Hides dependency direction and turns provider IDs into magic service keys. |
| Implicit fallback chain | Silently changes memory source and can cross an agent allowlist. |
| Generic repository over all memory records | Erases domain/persistence operations and adds an abstraction without a real alternate boundary. |
| Mediator/event bus for simple in-process delegation | Adds indirection without solving current ownership; reconsider only for proven cross-process/durable events. |
| Inheritance hierarchy for provider drivers | Transport behavior composes better through the driver port plus request/response/invoker collaborators. |
| Automatic mapping/reflection for protocol DTOs | Hides security-sensitive context omission and schema drift. Explicit mapping is required. |
| New project for every helper | Project extraction is justified only by stable ownership/dependency direction. Folders/namespaces and internal types are preferred for local cohesion. |

