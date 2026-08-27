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
3. SB03: normal local browser Simple Chats access, scoped identity and API security proof.
4. SB04: consistent avatars, canonical fresh-provider counts and isolated manual client.

## Dependency And Validation Map

See [phase plan](plan/01-phase-plan.md), [requirements](requirements/01-normalized-requirements.md)
and [execution report](reviews/01-execution-report.md). SB01-SB03 retain Governed proof;
SB04 uses the bounded Behavioral tier.
No unfiltered suite or sibling-repository changes were needed.

## UI Target Policy

1920x1080 desktop. Existing provider list/editor tabs and agent/chat dialogs.
Editor/dialog owns vertical scrolling; the price table and native model dropdown own
their overflow. No responsive redesign or shared component-library change.

## Validation Summary

- Bundle preparation status: Passed.
- Execution status: Completed, including SB04 avatar consistency and fresh-client handoff.
- Subbundle gate review: SB01 Passed; SB02 Passed; SB03 Passed; SB04 Passed.
- Final closure gate: Behavioral checks pass; latest canonical validation is in SB04 proof.
- Browser validation analytics: all three build6 real UI tests passed. OpenAI has 128
  discovered IDs, the image profile five, and Ollama 72 installed IDs. Full identity,
  all nine price fields and private-flag equality are asserted, not only counts.
- Runtime: OpenAI/Ollama simple chats and agents, approved image generation and vision
  passed; eight source invocations succeeded with complete token/image usage.
- Deployment: 5210/5212/5214 run avatar-blank-client-20260827-2 and return HTTP 200 Healthy.
- SB04: 36 focused cases pass; fresh client has zero providers/sources/imports/secrets
  after recreation. Existing volumes/history preserved; see proof/SB04/manifest.md.
- Normal-browser access: 50 focused tests pass; client OpenAI/Ollama and source OpenAI
  chats were created, executed and reloaded without browser JWTs. API authorization remains intact.

## Handoff

- Source: http://localhost:5210/agents?tab=providers
- Client: http://localhost:5212/agents?tab=providers
- Fresh manual client: http://localhost:5214/agents?tab=providers
- [Exact manual connection instructions](subbundles/04-avatar-and-fresh-client/HANDOFF.md).
- Details, exact test scopes, evidence and limitations:
  [execution report](reviews/01-execution-report.md).
- The validation source JWT is scoped and expires after four hours. Renew it through
  source Settings/API and update the client source secret for later testing.
