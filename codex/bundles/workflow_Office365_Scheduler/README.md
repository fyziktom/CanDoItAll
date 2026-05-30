# CanDoItAll Workflow Office365 Scheduler Follow-up

Prepared: `2026-05-29`  
Target branch: `processes-hardening`  
Observed head: `b70b7f4d0f5402df9980c0c3521bbc6e90b7badc`

## Validation Summary

- Bundle preparation status: `Valid for execution`
- Bundle readiness gate: `Passed`
- Execution status: `Completed with SB09 repair`
- Subbundle gate review: `Passed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`

## Mission

Deliver the next hardening and feature phase for the workflow executor catalog work:

1. Office365 email polling by concrete email address.
2. Managed workflow templates that summarize matching email or create project task nodes.
3. Automatic add-only marking of processed Office365 messages with a configured Outlook category.
4. Scheduler Planner readiness for recurring workflow runs, including user-friendly parameter entry for email address or CRM contact, project, and parent project-structure node.
5. Production-grade guardrails for polling behavior, idempotency, retry, approval, and no-message runs.
6. Unattended email processed-marker execution for Office365/Gmail workflows without weakening approval requirements for generic external writes.

## Current State Verdict

The previous executor-catalog bundle moved the workflow runtime forward significantly. The remaining gap is now a concrete business workflow pattern:

> Every two hours, check whether a specific person sent me a new unprocessed Office365 email, summarize it or create tasks under a chosen project/node, and then mark that email as processed.

That requires coordinated changes in the Office365 plugin executors, workflow templates, Scheduler Planner input contract and UX, scheduler dispatch semantics, and idempotent project writes.

## Recommended Execution Order

1. `subbundles/01-current-state-regression-and-gap-baseline`
2. `subbundles/02-office365-message-by-address-unprocessed-executor`
3. `subbundles/03-office365-email-summary-and-task-template-workflows`
4. `subbundles/04-scheduler-workflow-input-contract-and-template-parameter-schema`
5. `subbundles/05-scheduler-crm-email-project-node-picker-ux`
6. `subbundles/06-scheduled-polling-semantics-no-message-and-idempotency`
7. `subbundles/07-scheduler-dispatch-observability-and-retry-policy`
8. `subbundles/08-final-e2e-scenario-harness-and-browser-proof`
9. `subbundles/09-email-processed-marker-unattended-policy`

## Non-negotiable Guardrails

- Do not make live Graph calls in automated tests.
- Do not require destructive external effects during preview.
- No scheduled no-message poll may be treated as an error by default.
- Do not let retries duplicate project tasks or summary assets for the same Graph message.
- Keep `command.process` planned/unavailable unless a separate command sandbox bundle explicitly hardens it.
- Email processed-marker mutations for Office365/Gmail may run unattended only when the executor declares the idempotent external marker capability; generic external writes and host commands still require explicit approval.

## Bundle Layout

- `inputs/` raw request and source artifacts
- `analysis/` current state, findings, assumptions, and reopen triggers
- `requirements/` normalized requirements
- `architecture/` target solution and flow
- `plan/` execution order, dependency map, critical subbundles, and phase gates
- `traceability/` requirement ownership
- `subbundles/` execution-ready workstreams
- `proof/` per-subbundle proof manifests, semantic invariants, transcripts, screenshots, and verifier artifacts
- `reviews/` execution report and closure audit
