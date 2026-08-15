# Architecture gate

Before-change snapshot: `snap-20260815070334-363bd134`  
After-change snapshot: `snap-20260815072303-363bd134`

## CodeAnalytics result

- scoped projects: `CanDoItAll.Web`, `CanDoItAll.Modules.Workspace`,
  `CanDoItAll.Modules.LlmChats`, and `CanDoItAll.Modules.LlmChats.Persistence`;
- after-change documents/types/members: 191 / 609 / 4,140;
- DI registrations: 53;
- project direction: Web -> product and Workspace; Persistence -> product; product and Workspace have
  no project references in the scoped graph;
- cycles: 0;
- blocking diagnostics: 0;
- findings: 182 existing heuristic findings;
- open questions: one existing DI collector ambiguity in Workspace registration;
- production partial-class expansion: none;
- dormant deployment/participant model: none.

The three diagnostics are two duplicate embedded-type warnings and the existing informational Workspace
factory-registration collector ambiguity. They are unchanged, nonblocking, and unrelated to SB10. No
cycle, reverse dependency, persistence leakage, or new architectural owner was introduced.

## Manual architecture review

Status: Pass.

- Workspace owns reusable scope constants; Web owns authorization policy composition and route metadata;
- exact LLM Chat scopes deliberately do not use the existing broad-scope helper;
- authorization metadata remains conditional through the established endpoint extension, preserving
  auth-disabled local hosts without a fallback policy or hidden bypass;
- Web request/response records remain the transport boundary and map to product commands/primitives;
- product services retain provenance and lifecycle invariants without referencing Web or Workspace;
- raw exceptions no longer cross the LLM Chat logging boundary; stable product codes remain durable;
- no new interface, façade, partial class, project reference, provider dependency, or database schema
  was introduced;
- ADR-H11 records exact scopes, provenance, transport versions, redaction, and the bounded decision to
  defer conversation-create idempotency until the separate deployment identity boundary exists.
