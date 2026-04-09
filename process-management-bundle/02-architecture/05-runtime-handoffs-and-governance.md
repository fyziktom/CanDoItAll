# Runtime, handoffs, and governance

The runtime remains sequential-first, but it now carries stronger orchestration semantics.

## Runtime must understand

- ordered handoffs
- approvals and escalations
- unresolved staffing
- input-quality failures
- approved variants
- exception paths
- explicit decision-right rules
- wait reasons and blocked reasons
- normalized work briefs
- governed triage / routing decisions
- future external executor correlations

## Process-native work brief

Every executable step should be able to produce a normalized **work brief** from:

- process owner / customer / value context
- step contract
- role or template snapshot
- interface expectations
- evidence requirements
- risk tier / approval posture
- due or SLA expectations
- escalation context
- typed project/business references

This is the packet that a human or future AI executor should receive.

## Baton handoff

A handoff is not only a transition; it should produce a durable baton artifact that captures:

- source role / assignment
- target role / assignment or pool
- work brief snapshot
- why the baton is being passed
- the completion / handoff state of the source step
- correlation ids for replay and future runtime adapters

## Governed triage

Triage or dispatch is allowed, but it must remain visible and governed:

- triage can be modeled as a role, step, or routing policy
- routing choices should create a `ProcessTriageDecisionRecord`
- break-glass direct routing outside the process requires explicit override journaling

## Required journal evidence

Every significant runtime change should emit explicit journal events for:

- step activated
- step claimed
- work brief issued
- triage decision recorded
- step input rejected
- decision requested / applied
- approval requested / approved / rejected
- handoff created / accepted
- wait entered / wait exited
- exception triggered
- variant used
- rework started
- external executor linked / updated (future seam)
- run blocked / resumed / failed / completed

## Governance posture

Process governance should not be hidden in runtime-only config. The runtime needs direct access to the canonical metadata that answers:

- who may decide
- what evidence is required
- which controls are mandatory for this risk tier
- who receives escalations
- and whether a future AI executor is allowed to act without further approval
