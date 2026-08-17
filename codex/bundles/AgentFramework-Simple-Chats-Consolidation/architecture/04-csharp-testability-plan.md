# C# testability plan

## Contract

Every extracted behavior must be directly constructible and testable through its new owner without:

- AppDbContext for Core/Application/Runtime unit tests;
- the full Agent module;
- Web host startup;
- a file workspace plus EF database for neutral aggregation tests;
- reflection/service location;
- the old Modules.LlmChats assembly.

## Foundation proof

SB02:

- construct usage aggregation from in-memory IProviderUsageProjectionSource fakes;
- prove Agents, SimpleChats, Both, invalid None, partial source failure, unknown usage, and unpriced cost;
- prove aggregation is commutative and does not mutate source collections.

SB03:

- instantiate Core aggregates and Application services with narrow fakes;
- architecture tests reject EF/Razor/Web/Module references;
- negative test detects a shallow old-namespace delegation facade.

SB04:

- test provider resolver, completed/streaming adapters, decorators, and engine with Application ports;
- prove Runtime project has no EF/AppDbContext reference;
- profile failure/cancellation behavior remains explicit.

SB05:

- use relational integration for rows/configurations/repositories/leases/profile fences;
- prove new pricing evidence is atomic and rollback-safe;
- prove namespace move generates no table rename/drop;
- prove transfer round trip and deterministic legacy status.

SB06:

- test Agent and Simple Chat source adapters separately;
- test aggregate query with both adapters and duplicate attempt identities;
- prove the Agent execution service delegates to a top-level usage assembler and the old partial shrinks.

## UI proof

SB07 component tests construct Components with gateway fakes only. They exercise typed Identity/Runtime/Output-and-revision tab selection and the shared avatar selector independently of either editor. Agent and Simple Chat consumer tests prove the same component handles bundled selection, default reset, validated upload, deterministic AI success, unavailable provider, generation error, and save-only persistence semantics.

SB08 tests typed route/tab parsing, /chats redirect, no duplicate navigation/shell registration, and the reusable workspace without persistence/runtime.

SB09 tests one selection instance propagates to metrics, charts, consumer panels, and all detail dialogs. Negative cases include stale response after scope change, partial source failure, and invalid query.

## Required direct negative cases

- Core cannot reference Application/EF/Razor.
- Runtime cannot reference Persistence/AppDbContext.
- Components cannot reference Runtime/Persistence/Agent module/Web.
- SimpleChats.Components cannot duplicate avatar option/upload/generation markup or depend on the Agent module; AgentDetailsDialog cannot retain its old inline avatar selector.
- Usage cannot reference either store implementation.
- Duplicate (OperationId, Ordinal) cannot increase totals.
- Transcript and terminal aggregate cannot enter the source adapter.
- Legacy known tokens with no pricing cannot produce known $0.
- ChatSessionId/BasicChat cannot classify a workload.
- /chats cannot render a second workspace or register navigation.
- the Simple Chat editor cannot render a raw avatar URL input and cannot hide required validation or footer actions behind an inactive settings tab.
- selection None/unknown flags cannot execute a query.

## Old-owner shrink proof

At each architecture subbundle capture:

- moved type list and direct source location;
- old type/file line and member count before/after;
- remaining callers of old namespaces/projects;
- no-new-partial source scan;
- no-new-caller source scan;
- architecture gate verdict.

“The tests pass through the old facade” is insufficient.
