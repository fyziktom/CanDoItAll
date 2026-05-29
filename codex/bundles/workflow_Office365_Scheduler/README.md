# CanDoItAll Workflow Office365 Scheduler Follow-up

Prepared: `2026-05-29`  
Target branch: `processes-hardening`  
Observed head: `7bdbd7f70eeee2b357b28dfc9afc1c89fd9d5177`

## Mission

Review the pushed workflow executor catalog implementation and deliver the next hardening/feature phase focused on:

1. Office365 email polling by concrete email address.
2. Managed workflow templates that summarize or create tasks from the matching email.
3. Automatic marking of processed Office365 messages with a configured Outlook category.
4. Scheduler Planner readiness for recurring workflow runs, including user-friendly parameter entry for email address / CRM contact, project, and parent project-structure node.
5. Production-grade guardrails for polling behavior, idempotency, retry, and no-message runs.

## Current State Verdict

The previous executor-catalog bundle moved the workflow runtime forward significantly: MAF 1.8 is in place, catalog-backed validation is fixed, artifact content storage exists, and new built-in executors cover storage, JSON transforms, Markdown rendering, delay, approval, HTTP download, and source ingestion.

The next gap is not a generic executor problem anymore. It is the first real business workflow pattern:

> "Every two hours, check whether a specific person sent me a new unprocessed Office365 email, summarize it or create tasks under a chosen project/node, and then mark that email as processed."

This requires coordinated changes in Office365 plugin executors, workflow templates, Scheduler Planner UX, scheduler dispatch semantics, and idempotency.

## Recommended Execution Order

1. `subbundles/01-current-state-regression-and-gap-baseline`
2. `subbundles/02-office365-message-by-address-unprocessed-executor`
3. `subbundles/03-office365-email-summary-and-task-template-workflows`
4. `subbundles/04-scheduler-workflow-input-contract-and-template-parameter-schema`
5. `subbundles/05-scheduler-crm-email-project-node-picker-ux`
6. `subbundles/06-scheduled-polling-semantics-no-message-and-idempotency`
7. `subbundles/07-scheduler-dispatch-observability-and-retry-policy`
8. `subbundles/08-final-e2e-scenario-harness-and-browser-proof`

## Non-negotiable Guardrails

- All code comments must be in English.
- Do not introduce background claims without proof. Capture restore/build/test/browser evidence in `proof/`.
- Do not make live Graph calls in automated tests. Use fake `HttpMessageHandler`, fake `Office365GraphClient` seam, or deterministic plugin test mode.
- Do not require destructive external effects during preview. Preview must simulate Office365 download and category mutation.
- No scheduled "no matching email" poll may be treated as an error by default.
- Do not let retries duplicate project tasks or summary assets for the same Graph message.
- Keep `command.process` planned/unavailable unless a separate command sandbox bundle explicitly hardens it.
