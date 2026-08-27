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
5. SB05: compact provider toolbar/filter and shared connections dialog.
6. SB06: scope picker, durable token lifecycle and fresh 5214 handoff.

## Dependency And Validation Map

See [phase plan](plan/01-phase-plan.md), [requirements](requirements/01-normalized-requirements.md)
and [execution report](reviews/01-execution-report.md). SB01-SB03 retain Governed proof;
SB04 uses the bounded Behavioral tier.
SB01-SB04 did not need unfiltered suites. SB06 conservatively expanded to both Unit and
Integration after CodeAnalytics could not fully resolve dispatch. Broader failures are
reported separately from the passing requested behavior. No sibling source changes.

## UI Target Policy

1920x1080 desktop. Existing provider list/editor tabs and agent/chat dialogs.
Editor/dialog owns vertical scrolling; the price table and native model dropdown own
their overflow. No responsive redesign or shared component-library change.

## Validation Summary

- Bundle preparation status: Passed.
- Execution status: SB01-SB06 completed.
- Subbundle gate review: SB01-SB06 Passed for their requested behavioral/architecture scope.
- Final closure gate: Passed for requested behavior; broader suite failures remain explicit.
- Browser validation analytics: all three build6 real UI tests passed. OpenAI has 128
  discovered IDs, the image profile five, and Ollama 72 installed IDs. Full identity,
  all nine price fields and private-flag equality are asserted, not only counts.
- Historical SB02 runtime: OpenAI/Ollama simple chats and agents, approved image generation and vision
  passed; eight source invocations succeeded with complete token/image usage.
- Deployment: 5210/5212/5214 run admin-dialogs-20260827-2 and return HTTP 200 Healthy.
- SB05/SB06: 40 focused cases pass; actual desktop Playwright MCP confirms compact controls,
  modal connections, exact scope selection, lazy token management and live revoke/delete denial.
  5214 was reset recoverably and remains empty. See proof/SB06/manifest.md.
- Broader runs completed: Unit 6988 pass/1 fail; Integration 1121 pass/17 fail/1 skip.
  Unchanged fixture failures and scope limits are in proof/SB06/broad-regression-results.md.
  No clean-whole-repository claim is made.
- SB04: 36 focused cases pass; fresh client has zero providers/sources/imports/secrets
  after recreation. Existing volumes/history preserved; see proof/SB04/manifest.md.
- Normal-browser access: 50 focused tests pass; client OpenAI/Ollama and source OpenAI
  chats were created, executed and reloaded without browser JWTs. API authorization remains intact.

## Handoff

- Source: http://localhost:5210/agents?tab=providers
- Client: http://localhost:5212/agents?tab=providers
- Fresh manual client: http://localhost:5214/agents?tab=providers
- [Exact manual connection instructions](subbundles/06-token-lifecycle-and-fresh-handoff/HANDOFF.md).
- Details, exact test scopes, evidence and limitations:
  [execution report](reviews/01-execution-report.md).
- The renewed 5212 source JWT is scoped and expires after eight hours. Renew it through
  source Settings/API and update the client source secret for later testing.
