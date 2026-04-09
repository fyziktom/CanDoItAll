# Process-native orchestration and baton handoffs

This document closes the main ambiguity raised by the latest review pass:

> future human and AI collaboration should be wired through the modeled process, not beside it.

## Canonical orchestration rule

The process definition is the canonical graph of:

- who may do work
- in what order
- under what contract
- with what approvals
- with what handoff payload
- and under what routing or escalation rules

## Work brief model

Every executable step should be able to produce a work brief that includes:

- process identity and version
- run and step context
- process owner and customer context
- value statement / criticality / risk tier
- step contract
- input/output/evidence requirements
- actor template snapshot
- due / SLA expectations
- escalation and approval expectations
- typed project or business references

## Baton lifecycle

1. Step becomes active.
2. Work brief snapshot is created.
3. Source actor performs work or prepares handoff.
4. Baton handoff record is created.
5. Target actor or pool accepts, rejects, or reroutes.
6. Journal captures the transition with reasons and correlation ids.

## Triage rule

A triage or dispatch role is allowed, but it is still process work:

- it should be modeled as a process role, node, or routing policy
- it should create durable routing decisions
- it should not become a hidden direct-chat topology among agents

## Break-glass escape hatch

If production work must bypass the modeled process in an emergency, the system should require:

- explicit override reason
- owner or policy review path
- durable journal evidence
- later conformance visibility

That keeps exceptions visible instead of silently normalizing them.
