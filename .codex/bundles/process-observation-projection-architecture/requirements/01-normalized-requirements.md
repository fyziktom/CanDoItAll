# Normalized Requirements

| ID | Requirement | Source | Acceptance |
| --- | --- | --- | --- |
| R-001 | Introduce a read-only process observation boundary between process core and UI. | User request | UI and future dashboard code can request observation snapshots without directly composing runtime, outbox, AgentFramework, and escalation data in the component. |
| R-002 | Preserve all current Processes page functionality. | User request | Existing authoring, launching, runtime controls, details, analytics, manager chat, and canvas tests still pass after implementation. |
| R-003 | Keep process runtime logic generic. | User request | Observation services do not encode app-specific process semantics beyond existing typed process models. |
| R-004 | Support multi-process live observation without overload. | User request | Dashboard queries are bounded, coalesced, cancellable, and validated at scale. |
| R-005 | Use `IMemoryCache` only as a projection cache. | User request and Microsoft Learn caching guidance | Cache has bounded keys, size/TTL policy, explicit staleness metadata, and a source read fallback. |
| R-006 | Avoid split source of truth. | User request | Runtime writes update persistence first; observation projections are derived and invalidated after authoritative changes. |
| R-007 | Make detail drill-downs lazy and typed. | User request | Dialogs open from descriptors and load only the requested run/stage/artifact/QA/detail payload. |
| R-008 | Align with Blazor performance guidance. | Microsoft Learn MCP | Large lists use virtualization/windowing, state containers are granular, background updates use `InvokeAsync`, and high-frequency renders are throttled. |
| R-009 | Prepare future AI-driven dashboard focus. | User request | AI output is represented as typed read-only observation intents that resolve to filters, focus targets, and dialog descriptors. |
| R-010 | Preserve existing lazy-loading improvements. | Current code and previous bundle | No implementation reloads full details for all active runs or analytics while hidden. |
| R-011 | Add performance and regression validation. | User request and performance skill | Validation covers mock agents, generic process cases, simple independent .NET app builds, component tests, and browser proof for UI phases. |
| R-012 | Make operational failures visible. | AGENTS.md and architecture principles | Observation failures produce explicit error/staleness state and actionable logs; they are not silently hidden by cache fallback. |
