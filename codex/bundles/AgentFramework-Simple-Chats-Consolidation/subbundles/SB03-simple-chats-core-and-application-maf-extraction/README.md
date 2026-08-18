# SB03 — Simple Chats Core and Application MAF extraction

## Status

- Completed — Pass
- Stage: boundaries
- Proof tier: Governed

## Objective

Move Simple Chat domain behavior into Core and use cases/ports into Application under the MAF feature family without changing public behavior, schema, HTTP contracts, or execution ownership.

## Owned Requirements

- ASCC-002
- ASCC-006
- ASCC-007
- ASCC-008
- ASCC-009
- ASCC-014
- ASCC-016
- ASCC-017
- ASCC-045
- ASCC-046

## Prerequisites

- SB02

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.LlmChats/Common/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/Definitions/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/Conversations/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/Operations/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/Ports/
- repo://src/Modules/CanDoItAll.Modules.LlmChats/LlmChatsModuleServiceCollectionExtensions.cs

## Explicit Non-Goals

- Do not move provider runtime, EF implementations, Razor, routes, or navigation.
- Do not alter state transitions, API serialization, authorization, or DB mappings.
- Do not create a permanent old-namespace facade.
- Do not split large domain classes solely by line count.

## Implementation Steps

1. Create Core and Application target projects with the exact allowed references.
2. Add failing architecture tests for forbidden EF/Razor/Web/Module dependencies.
3. Move strong IDs, aggregates, operations, events/reducers/transitions, validation, and fingerprints to Core.
4. Move commands/results, services, dispatcher/executor/state-machine orchestration, event sessions, and ports to Application.
5. Move Application DI to AddSimpleChatsApplication with explicit registration cardinality tests.
6. Update Persistence, UI, API, and tests to target new Core/Application namespaces in one compile-safe cutover.
7. Preserve serialized names, HTTP route templates, API scopes, error codes, and SSE event shape.
8. Use a temporary facade only if CP0 proved an external binary consumer; add no-new-caller guard and SB10 deletion marker.
9. Delete the old core project when caller inventory reaches zero; do not leave a forwarding assembly.
10. Capture old/new type inventory and direct behavior ownership proof.

## Acceptance Criteria

- [x] Core and Application build/test independently of EF, Razor, Web, Agent module.
- [x] All Core/Application behavior resides in target projects.
- [x] Existing API/application selectors remain green.
- [x] Old core project has zero callers and is removed; no facade exists.
- [x] No cycle/partial/shallow delegation.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; Runtime, Persistence, Components, APIs, and tests consume these owners.

Governed critical extraction with before/after dependencies, type/caller inventories, direct tests, negative architecture assertions, old-owner shrink/removal, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx

Required:

- LlmChatApplicationBoundaryTests
- LlmChatCanonicalModelTests
- LlmChatDefinitionServiceTests
- LlmChatConversationApplicationServiceTests
- LlmChatOperationTests
- LlmChatDurableStreamEventTests
- LlmChatsDefinitionApiIntegrationTests
- LlmChatsConversationApiIntegrationTests
- LlmChatsSecurityApiIntegrationTests
- LlmChatApiValidationIntegrationTests
- LlmChatApiPrivacyIntegrationTests
- LlmChatApiMetadataIntegrationTests
- LlmChatOperationStorageContractIntegrationTests

Add SimpleChatArchitectureBoundaryTests with exact CoreHasNoOuterDependencies and ApplicationUsesOnlyCoreAndPorts cases.

Expected discovery: non-zero for every class and both exact new cases.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on Core/Application public contract, operation transition, port, DI, API mapper, namespace facade, or project reference change.

## UI Composition Contract

No visual change. Existing UI must render through updated contracts with identical behavior.

## C# Architecture Impact

Critical project-boundary extraction of roughly half the existing feature surface.

## Boundary Ownership

Core owns invariants; Application owns orchestration/ports. Old Modules.LlmChats cannot remain the effective behavior owner.

## Dependency Direction

Application -> Core. Core never references Application. Both remain inward of Runtime/Persistence/Components/Web.

## Pattern Decision

Layered feature libraries and ports/adapters. No extra Abstractions assembly without CP0 evidence.

## Testability Contract

Application use cases must run with narrow fakes and no full old runtime. A negative architecture test must fail if an EF/Razor/Module reference returns.

## Partial Class Policy

No new partial. Existing non-Razor partials touched by the move must shrink or be replaced by top-level collaborators.

## Architecture Proof Required

Before/after dependencies/cycles, direct source ownership, direct tests, shallow-delegation negative, no-new-partial, old-owner caller count/size, review gate.

## Progression Gate

- Core/Application extraction and tests pass before Runtime moves in SB04.

## Reopen Triggers

- remaining behavior in old project;
- API/serialization/state transition drift;
- outer dependency in Core/Application;
- new facade caller;
- DI duplication or cycle.

## Covered Inputs

- Raw request: isolate basic Simple Chat abstractions/helpers rather than adding all classes to Agent module; move feature ownership under MAF.
- Requirements ASCC-002, ASCC-006–009, ASCC-014, ASCC-016–017, ASCC-045–046.

## Exact Source References

- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats\Common
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats\Definitions
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats\Application
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats\Ports

## Deliverables

- MAF SimpleChats.Core and SimpleChats.Application with direct behavior ownership and migrated callers.

## Dependency Impact

- SB04-SB10 depend on stable Core/Application contracts; any facade or outer dependency invalidates those phases.

## Acceptance Checklist

- All Acceptance Criteria above pass and the old core owner is removed or CP0-authorized/bounded.

## Proof Required

- proof/SB03/manifest.md, type/caller/hash inventories, failing/passing direct tests, API compatibility, forbidden-reference/no-new-partial guards, architecture gate.
