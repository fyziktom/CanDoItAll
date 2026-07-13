# Task 10: Observability and UI diagnostics

## Goal

Operators should not need to inspect raw MAF logs to understand why a process routed repair or escalated.

## Add evaluation trace

Persist a trace for every completion gate evaluation:

- step key,
- branch outcome key,
- current execution run id,
- applicable receipt rules,
- skipped receipt rules and why,
- observed receipt summary,
- content checks evaluated/skipped,
- issues grouped by route kind,
- final route decision.

## UI/operator summary

For this incident, desired message would be similar to:

> QA selected repair-required because deterministic scaffold content remained. Acceptance-only browser proof receipts were skipped for the repair branch. The process routed to quality-repair.

Or when missing proof is real:

> QA claimed quality-accepted but did not execute required browser proof. Same-step retry is required; this is not an implementation repair branch.

## Acceptance

- `No AgentFramework result summary` is not used as the primary operator explanation for runtime-owned branch routing.
- Diagnostics can distinguish absent receipt, wrong execution run, failed receipt, and skipped-by-branch receipt.
