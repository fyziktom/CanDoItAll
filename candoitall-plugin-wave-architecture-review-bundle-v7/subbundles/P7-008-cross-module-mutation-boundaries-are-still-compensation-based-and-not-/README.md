# P7-008 - Cross-module mutation boundaries are still compensation-based and not ready for outbound connector side effects

- Severity: Medium-High
- Gate: Conditional blocker
- Status: Open
- Repeated from: PW6-008

## Problem

Delete and move flows still persist Workbench changes first and reconcile CRM/HR afterward with rollback-on-failure logic. That is survivable internally, but it is not a safe base once email, LinkedIn, or custom API plugins begin performing outbound or externally visible actions.

## Required direction

Before allowing connectors to perform outbound or destructive side effects, introduce an explicit mutation boundary: single-transaction orchestration where possible, otherwise durable outbox/saga patterns with replay and recovery state.

## Closure proof

A documented and tested mutation boundary exists; outbound connectors are forbidden until that boundary is in place.
