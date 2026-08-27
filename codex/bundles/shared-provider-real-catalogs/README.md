# Real provider catalogs and faithful sharing

## Mission

Make the existing source (5210) and client (5212) reflect real upstream providers:
actual model IDs, authoritative catalog membership and honest prices.

## Outcome Contract

- Source and client show the same full real model names, prices and private flag.
- Discovery replaces stale membership; a price list cannot invent models.
- Unknown prices remain absent, not fabricated zero rates.
- Setup and execution are validated through the UI against real OpenAI and Ollama.
- Existing IDs, volumes, histories, unrelated work and port 5032 are preserved.
- Previous fixture-only acceptance is historical, not evidence for this bundle.

## Recommended Execution Order

1. SB01: authoritative catalog/pricing refresh, kind isolation and reopened runtime defects.
2. SB02: rebuilt pair, real UI setup, full parity, execution, source usage and health.

## Dependency And Validation Map

See [phase plan](plan/01-phase-plan.md), [requirements](requirements/01-normalized-requirements.md)
and [execution report](reviews/01-execution-report.md). Both phases retain Governed proof.
No unfiltered suite or sibling-repository changes were needed.

## UI Target Policy

1920x1080 desktop. Existing provider list/editor tabs and agent/chat dialogs.
Editor/dialog owns vertical scrolling; the price table and native model dropdown own
their overflow. No responsive redesign or shared component-library change.

## Validation Summary

- Bundle preparation status: Passed.
- Execution status: Completed.
- Subbundle gate review: SB01 Passed; SB02 Passed.
- Final closure gate: Passed; canonical completed-stage validator exit 0.
- Browser validation analytics: all three build6 real UI tests passed. OpenAI has 128
  discovered IDs, the image profile five, and Ollama 72 installed IDs. Full identity,
  all nine price fields and private-flag equality are asserted, not only counts.
- Runtime: OpenAI/Ollama simple chats and agents, approved image generation and vision
  passed; eight source invocations succeeded with complete token/image usage.
- Deployment: both hosts run real-catalogs-20260827-6 and return HTTP 200 Healthy.

## Handoff

- Source: http://localhost:5210/agents?tab=providers
- Client: http://localhost:5212/agents?tab=providers
- Details, exact test scopes, evidence and limitations:
  [execution report](reviews/01-execution-report.md).
- The validation source JWT is scoped and expires after four hours. Renew it through
  source Settings/API and update the client source secret for later testing.
