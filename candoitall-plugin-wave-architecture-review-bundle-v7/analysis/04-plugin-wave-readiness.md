# Plugin-wave readiness

## Decision

**NO-GO**

The current branch is still not a safe base for the next big connector/plugin wave.

## Hard blockers

- P7-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth
- P7-002 - The universal node carrier is still overloaded instead of being a stable carrier plus typed facets and bindings
- P7-003 - Node-kind semantics and node-scoped capability rules are still fragmented and hardcoded
- P7-004 - Node reclassification still mutates in place without transition history or facet supersession
- P7-005 - Hierarchy is still dual-represented through ParentNodeKey and generic link rows
- P7-006 - Workbench metadata still carries foreign identifiers and keeps dual marker truth
- P7-007 - Provider/resource/connector architecture is still a closed enum-and-switch seam
- P7-010 - There is still no hard architecture closure mechanism preventing the same blockers from being reintroduced

## Conditional blocker

- P7-008 - Cross-module mutation boundaries are still compensation-based and not ready for outbound connector side effects

## Watch items

- P7-009 - Workbench and CRM/HR service hotspots remain too large and multi-responsibility

## Key reason

The system is still too willing to treat projections, metadata helpers, and closed integration enums as if they were stable architecture. That is exactly the wrong base for a large connector wave.

## What is allowed right now

- small local bug fixes
- narrow UX polishing
- tests and guardrails
- isolated refactor work that closes the hard blockers

## What is not allowed right now

- large email connector work
- LinkedIn connector work
- general custom API plugin platform work
- any new feature that depends on the current closed ProviderKind / ResourceKind seam
