# 36 Selection Authorization And Application Modularization

## Status

- `Completed`

## Execution Outcome

- Provider selection is explicit and fail-closed: allowed provider IDs, named defaults/assignments, and fallback policy are enforced without registry-first or sole-provider dispatch.
- Operation access compares the persisted requester, agent, role, session, workflow/node, and process/step owner before status or cancellation disclosure/mutation.
- `MemoryOperationHandler` is a non-partial facade over cohesive top-level application services; Application owns its DI registration and shared source contracts moved to `CanDoItAll.Memory.SourceGateway.Abstractions`.
- The repaired Memory/Application/MAF Memory target has no prohibited handwritten capability-grouping partials and Memory Application no longer references Agent Framework Core.
- SB40 supplied the terminal confirmation: the generic Memory aggregate passed 196 tests with one intentional live-env skip, and the live external test passed separately.

## Objective

- Make generic provider selection deny-by-default, bind operation lifecycle access to the originating caller, and replace capability-grouping handler partials with cohesive application services behind the existing operation-handler boundary.

## Success Criteria

- A request can select only an enabled, capability-compatible provider explicitly allowed by its policy; deny-fallback never chooses the first registered provider.
- Ambiguous, missing, disabled, disallowed, and capability-incompatible selections return distinct typed results without dispatching a driver.
- Status and cancellation require an authorized requester/agent/session scope and cannot disclose or mutate another operation.
- `MemoryOperationHandler` is a small non-partial facade, and extracted services have narrow constructors and direct tests.
- Generic application registrations no longer originate in the Persistence project.

## Covered Inputs

- R02
- R04
- R09
- R10
- R20
- R25
- R27

## Prerequisites

- SB35 completed with passing prepared-stage and C# architecture readiness gates.

## Exact Source References

- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://plan/architecture-checkpoints.md`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryProviderRegistryContracts.cs`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryOperationLedgerRecords.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/IMemoryOperationHandler.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationCoordinator.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryStatusOperationService.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceCaptureOperationService.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryFeedbackOperationService.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderRegistry.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderEventWorker.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderEventOutboxProcessor.cs`
- `repo://src/Memory/CanDoItAll.Memory.Persistence/MemoryPersistenceServiceCollectionExtensions.cs`
- `repo://src/Memory/CanDoItAll.Memory.Persistence/EfMemoryRetentionProjectionStore.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryProviderRegistryTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationHandlerTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryLedgerLifecycleTests.cs`

## Deliverables

- Extend the strongly typed selection policy with an allowed-provider set and explicit fallback semantics; reject implicit first-provider selection when fallback is denied.
- Return typed selection diagnostics for no provider, ambiguous provider, unknown explicit provider, disabled provider, disallowed provider, and capability mismatch.
- Persist the normalized requester identity required to authorize operation status and cancellation, including provider, agent, runtime session, workflow/process scope, and caller identity where present.
- Introduce a narrow `IMemoryOperationAuthorizer` boundary with a production implementation that compares normalized operation ownership and rejects missing identity where protected scope is required.
- Refactor `MemoryOperationHandler` into a non-partial facade delegating to cohesive top-level query, lifecycle, feedback/event, and source-capture services; keep one shared ledger and driver-dispatch path.
- Extract event inbox/outbox processing and retention projection responsibilities from capability-grouping partial classes into named top-level services or merge genuinely cohesive fragments.
- Add Application-owned DI registration and make Persistence register only persistence implementations plus a compatibility call to the Application registration if needed during migration.
- Rehome the shared `MemorySourceSnapshot*` contract family to a provider-neutral owner and migrate callers so Memory Application no longer references Agent Framework Core; retain one compatible snapshot family.
- Preserve public operation contracts unless a typed authorization/selection result is required; document each contract change and caller impact.

## Dependency Impact

- SB37 relies on deterministic, allowlisted selection and safe fan-out calls.
- SB38 relies on one application request shape and one error boundary for transport adapters.
- SB40 cannot close while status/cancel ownership or implicit fallback can bypass agent settings.

## Validation Depth

- `Critical generic-memory application foundation`

## C# Architecture Impact

- The current broad partial handler becomes a composition facade with use-case services sized around query, operation lifecycle, feedback/events, and source capture.
- Selection and authorization become explicit application policies instead of incidental helper logic.
- Application registration moves to the Application project, eliminating Persistence as the owner of application orchestration.

## Boundary Ownership

- Memory Abstractions owns provider IDs, caller/ownership value objects, selection policy contracts, and typed result statuses.
- Memory Application owns selection decisions, operation authorization, orchestration, ledger transitions, and worker use cases.
- Memory Persistence owns durable stores only and must not choose providers or authorize callers.
- Drivers remain transport implementations and receive only an already authorized, fully selected request.

## Dependency Direction

- `Memory.Application -> Memory.Abstractions` is allowed.
- `Memory.Persistence -> Memory.Application + Memory.Abstractions` is allowed for store implementations and registration.
- `Memory.Application -> Memory.Persistence`, Agent Framework, module UI, or native Cognitive Memory is forbidden.
- The facade may depend on cohesive application services; extracted services must not call back through the facade and create orchestration cycles.

## Pattern Decision

- Use Strategy for provider selection and operation authorization because behavior varies by explicit policy and caller scope.
- Use a thin Facade for `IMemoryOperationHandler` to preserve the shared entry point while delegating cohesive use cases.
- Use explicit constructor composition in an Application service-collection extension; do not use a service locator, static registry, or catch-all helper service.
- Do not introduce one-interface/one-trivial-class abstractions except for the real authorization boundary and externally substitutable stores/drivers.

## Testability Contract

- Provider selection is directly testable with an in-memory catalog and a recording driver that proves whether dispatch occurred.
- Authorization is directly testable with owned, same-agent/different-session, different-agent, missing-requester, and privileged/system cases.
- Each extracted handler service can be constructed with fakes and exercised without EF, HTTP, MCP, MAF, or a web host.
- Composition smoke tests resolve the facade and every worker from the real Application/Persistence registrations.

## Partial Class Policy

- Delete the `MemoryOperationHandler.*.cs` capability-grouping partial declarations; file names may remain only if they contain independent top-level types.
- Delete capability-grouping partials from `MemoryProviderEventWorker` and `EfMemoryRetentionProjectionStore` by extracting named collaborators or merging cohesive implementation.
- Generated, Razor, and EF migration partials remain out of scope and are allowed.

## Architecture Proof Required

- Show the post-change type and project dependency graph with no new cycles and no Memory Application dependency on Agent Framework or Persistence.
- Record constructor dependency counts and lines/member counts for the facade and extracted services; the facade must demonstrably shrink.
- Run an `rg` partial-class audit proving the prohibited generic application/persistence partials are gone.
- Add architecture tests that fail if Application registration is reintroduced into Persistence as its primary owner or if selection bypasses allowed-provider policy.

## Implementation Steps

1. Turn the SB35 fallback and cross-owner characterization tests into focused red tests with named typed outcomes.
2. Add or refine strongly typed caller ownership and selection-policy contracts in Memory Abstractions.
3. Implement deterministic selection with explicit allowlist and fallback handling before any driver is resolved.
4. Implement operation authorization and apply it to status and cancellation before returning ledger state or invoking a driver.
5. Extract the handler use cases and workers into cohesive top-level services while preserving one facade and one ledger transition model.
6. Move application DI ownership into Memory Application and update Persistence and callers.
7. Add direct unit, composition, concurrency, and persistence lifecycle tests; run dependency and partial audits.

## Scope Exceptions

- Agent settings, directive syntax, and multi-provider fan-out are owned by SB37.
- HTTP/MCP mapping and provider-profile editor changes are owned by SB38.

## Do Not Do

- Do not preserve backward compatibility by silently selecting the first provider.
- Do not treat provider ID alone as sufficient authorization for status or cancellation.
- Do not create a god `MemoryOperationService`, a generic `Helpers` class, or another partial-class cluster.
- Do not duplicate ledger writes in the facade and extracted handlers.

## Acceptance Checklist

- Deny-fallback with no explicit/default/assignment returns a typed no-selection result and records zero driver calls.
- An allowlist cannot be bypassed by a default, assignment, selection tag, or registration order.
- Ambiguous selection is deterministic and fails explicitly rather than choosing one candidate.
- Same owner can read/cancel its operation; different agent/session/requester and missing protected identity are denied without revealing operation details.
- Status does not report `Completed` when selection or authorization failed.
- The facade is non-partial and delegates to named cohesive services with direct tests.
- Generic Application and Memory test suites pass, and real DI can resolve all application services and workers.

## Proof Required

- Create `proof/SB36/manifest.md` and `proof/SB36/semantic-invariants.md` with hashes and portable source/transcript references.
- Failing-first proof: capture current implicit fallback and cross-owner status/cancel tests failing against pre-SB36 production code.
- Positive proof: prove explicit allowed selection dispatches exactly one intended driver and the operation owner can poll/cancel through the real facade and ledger.
- Negative proof: prove disallowed/default/ambiguous providers and foreign/missing owners produce typed denial and zero driver calls.
- Anti-stub proof: use recording drivers and the real operation ledger to correlate the selected provider, operation ID, owner, transition, and driver invocation; DTO-only selection assertions are insufficient.
- Run focused Memory tests, DI composition smoke tests, build gates, dependency graph audit, and prohibited-partial audit.

## Browser Validation Logging

- N/A. This subbundle changes application behavior but no browser-visible surface. Record N/A unless an implementation unexpectedly changes UI, in which case stop and re-scope.

## Progression Gate

- SB37 may start only after fallback, allowlist, ambiguity, operation ownership, facade composition, dependency, and partial-class proofs pass and the SB36 architecture checkpoint is recorded.

## Suggested Agent Prompt

```text
Implement SB36 only. Enforce deny-by-default provider selection and operation ownership, modularize the generic handler without duplicating orchestration, remove the listed capability-grouping partials, capture failing-first and anti-stub proof, and stop if the architecture checkpoint cannot pass.
```
