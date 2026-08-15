# Architecture gate

Before-change snapshot: `snap-20260815061427-041f6d4b`  
After-change snapshot: `snap-20260815064713-4eb8c3ec`

## CodeAnalytics result

- scoped projects: `CanDoItAll.Web`, `CanDoItAll.Modules.LlmChats`, and
  `CanDoItAll.Modules.LlmChats.Persistence`;
- after-change documents/types/members: 162 / 505 / 3,383;
- DI registrations: 37;
- project direction: Web -> product; Persistence -> product; product has no project reference;
- cycles: 0;
- workspace diagnostics: 0;
- open questions: 0;
- partial-class expansion: none;
- product Web/ASP.NET dependency: none;
- dispatch/invocation/cancel ownership in SSE projection: none.

The scoped finding count is 146 and consists of complexity heuristics across the selected existing Web
surface. No diagnostic, cycle, open question, reverse dependency, or blocking architectural defect was
reported. The changed behavior is split among small named owners; the generic writer remains the one
transport implementation for cursor, heartbeat, framing, anti-buffering, and lifetime behavior.

## Manual architecture review

Status: Pass.

- application owns the profile-fenced durable session and bounded read protocol;
- persistence owns SQL operation/event/range/aggregate queries and remains replay authority;
- Web owns route validation and normalized public projection only;
- SSE never dispatches, invokes, reconciles, abandons, or cancels an operation;
- disconnect and profile switch terminate only the projection/session lease;
- direct PostgreSQL proof resolves the new session owner from DI and calls it before/after switch;
- the 202 command-resource, GET status, GET events, and POST cancel resources share one operation ID;
- no trivial interface, façade over unchanged behavior, partial extraction, project, or dependency cycle
  was introduced;
- no architecture-plan deviation was needed. The SB09 implementation record documents the realized
  ADR-H10 boundary.
