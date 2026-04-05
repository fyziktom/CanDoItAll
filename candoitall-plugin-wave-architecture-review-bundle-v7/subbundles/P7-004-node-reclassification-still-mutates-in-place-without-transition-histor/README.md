# P7-004 - Node reclassification still mutates in place without transition history or facet supersession

- Severity: High
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-004

## Problem

ReclassifyObjectAsync still mutates the same row in place and the integration tests still validate that behavior. That preserves stable node identity, which is good, but it loses the semantically important evolution from quick note / brainstorm capture into richer operational structures.

## Required direction

Keep node identity stable, but write explicit ProjectNodeTransitionHistory and facet migration / supersession rules. Shared carrier fields may stay mutable, but kind transitions must be journaled and kind-specific facet payload must be archived or superseded instead of silently overwritten.

## Closure proof

Reclassification writes transition history, preserves stable node identity, and adds guardrail tests for same-family and cross-family transitions.
