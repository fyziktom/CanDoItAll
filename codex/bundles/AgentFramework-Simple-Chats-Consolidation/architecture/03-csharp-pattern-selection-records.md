# C# pattern selection records

## PSR-001 — Layered feature libraries

Decision: separate Core, Application, Runtime, Persistence, and Components.

Why: each has a distinct reason to change and a testable boundary. Current Persistence combines provider runtime with EF; current core combines invariant-bearing types with use-case orchestration.

Rejected:

- one renamed SimpleChats project: preserves mixed responsibilities;
- one project per large class: optimizes file count rather than cohesion;
- moving everything into the Agent module: creates a product-module god assembly.

## PSR-002 — Ports and adapters for operational stores

Decision: Agent and Simple Chat audit stores implement IProviderUsageProjectionSource independently.

Why: they have different consistency and storage models. The neutral Usage service composes read contributions but does not coordinate writes.

Rejected:

- EF-to-file dual write: non-atomic and retry-duplicating;
- UI-only merge: makes dialogs/API/export inconsistent and couples Razor to persistence;
- central ledger/outbox now: valid future architecture but materially beyond this initiative.

## PSR-003 — Immutable evidence at capture time

Decision: new Simple Chat invocation attempts persist token completeness, cost completeness, calculated/provider cost, and pricing provenance in the same relational transaction as the audit row.

Why: pricing changes; query-time calculation cannot reproduce historical truth.

Rejected:

- current-price backfill: false history;
- zero as unknown: false “free” cost;
- transcript as ledger: omits failed/retried attempts.

## PSR-004 — Typed atomic producer and flags selection

Decision:

- atomic producer kind: Agent or SimpleChat;
- validated selection flags: Agents, SimpleChats, Both;
- None and unknown bits fail predictably.

Why: Both is a query selection, never a persisted source. ChatSessionId and BasicChat are semantically ambiguous.

Rejected: string filters, display-label inference, nullable booleans, fake Agent IDs.

## PSR-005 — Composite read model

Decision: aggregate normalized source slices into provider, model, consumer, totals, completeness, and source-health views.

Why: provider/model totals combine naturally; consumer rankings require source-specific identity and labels.

Both view does not flatten Simple Chat definitions into Agent rows.

## PSR-006 — Compatibility route adapter

Decision: /chats is a thin redirect to /agents?tab=simple-chats with an explicit recognized-query map.

Why: bookmarks remain valid without rendering a second page or retaining duplicate navigation.

Rejected: embedding LlmChatsPage in AgentsHomePage, keeping two canonical workspaces, permanent namespace forwarding assemblies.

## PSR-007 — Strategy policy for scoped dashboard rendering

Decision: use typed selection and source-neutral rows, with small mapping/render policies for Agent versus Simple Chat consumer sections.

Why: avoids branching string logic throughout the Razor page and dialogs.

Do not introduce a general framework or one-implementation interface solely for tests; extract only when the behavior boundary is real.

## PSR-008 — Partial-class policy

Decision: no new partial class. Touched provider-usage behavior leaves AgentFrameworkWorkspaceExecutionService.Usage for top-level collaborators. Large UI/controller classes shrink through cohesive collaborators or component state services, not additional partial files.

