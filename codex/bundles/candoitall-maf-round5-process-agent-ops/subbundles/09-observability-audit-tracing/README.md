# 09 Observability, Audit, and Traceability

## Goal

Make agent/process behavior explainable after the fact.

## Tasks

1. Add correlation ids across process run, step run, execution run, tool receipts, recovery decisions, rework packets, escalations, and UI actions.
2. Log redacted structured output validation errors.
3. Log finalizer required/shadow/disabled status and result.
4. Log tool policy decision and exception boundary.
5. Log proof fingerprint validation/reuse/invalidation.
6. Add attempt timeline view model and tests.

## Acceptance criteria

- Every completion, retry, approval, escalation, and rework is reconstructable without reading raw chat history.
