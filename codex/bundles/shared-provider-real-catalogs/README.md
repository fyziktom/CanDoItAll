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
7. SB07: shared thinking capabilities, request enforcement and main model suggestions.
8. SB08: real multi-agent thinking proof; preserve all three instances.
9. SB09: provider model thinking configuration and stale import recovery.
10. SB10: actual source and both-client UI proof, including 5214.
11. SB11: actionable shared-access failures and project-context image recovery.

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
- Execution status: SB11 completed for N015; requested behavior and final regression review pass.
- Current incident: expired source JWT, lost safe status, opaque invalid image options,
  and image/text size-limit confusion are repaired. Real Calculator image creation,
  attachment, governed preview and shared image analysis passed; see proof/SB11/manifest.md.
- Subbundle gate review: Passed for requested behavioral/architecture scope.
- Final closure gate: N015 closed by reviews/05-project-image-final-verifier.md.
  N013/N014 remain closed by reviews/04-model-thinking-final-verifier.md; earlier
  verifiers remain historical evidence.
- SB11: 129 focused cases pass. Final Unit 7059 pass/1 fail; Integration 1133 pass/
  10 fail/1 opt-in skip. All failed identities and reviewed causes match SB09;
  see proof/SB11/broad-regression-results.md. The full repository is not green.
- SB07/SB08: 308 focused cases pass with exact discovery. Seven UI-configured agents
  completed nine real requests with expected independent efforts and complete source usage.
  Source/client real model labels, natural order and supported efforts match exactly.
  See proof/SB08/manifest.md, including the OpenAI Chat Completions transport limitation.
- Current deployment: all three hosts run shared-access-20260828-3, HTTP 200 Healthy, data intact.
  5214 was NOT reset in this follow-up. The history below describes earlier checkpoints.
- SB09/SB10: source-owned per-model Thinking settings, explicit stale import refresh,
  229 focused passing tests, exact agent/Simple Chat model choices and eight real
  requests with source usage. See proof/SB10/manifest.md and
  subbundles/10-model-thinking-ui-proof/HANDOFF.md. Broad failures remain separate.
- Browser validation analytics: all three build6 real UI tests passed. OpenAI has 128
  discovered IDs, the image profile five, and Ollama 72 installed IDs. Full identity,
  all nine price fields and private-flag equality are asserted, not only counts.
- Historical SB02 runtime: OpenAI/Ollama simple chats and agents, approved image generation and vision
  passed; eight source invocations succeeded with complete token/image usage.
- Historical SB06 deployment: admin-dialogs-20260827-2 (superseded by thinking-20260827-6).
- SB05/SB06: 40 focused cases pass; actual desktop Playwright MCP confirms compact controls,
  modal connections, exact scope selection, lazy token management and live revoke/delete denial.
  5214 was reset recoverably at that checkpoint. Later user setup is preserved. See proof/SB06/manifest.md.
- Historical SB06 broader runs: Unit 6988 pass/1 fail; Integration 1121 pass/17 fail/1 skip.
  Unchanged fixture failures and scope limits are in proof/SB06/broad-regression-results.md.
  No clean-whole-repository claim is made.
- Historical SB09 broad checkpoint: Unit 7037 pass/1 fail; Components 1110 pass/52 fail;
  Integration 1133 pass/10 fail/1 skip. Every failed identity occurs in SB07;
  exact reviewed causes and deferred-theory discovery are in proof/SB09/broad-regression-results.md.
- Historical SB07 broad checkpoint: Unit 7014 pass/1 fail; Components 1103 pass/53 fail;
  Integration 1121 pass/18 fail/1 skip. Related failures were repaired in the final
  focused scope; exact limits are in proof/SB07/broad-regression-results.md.
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
- The recovered 5214 source JWT expires 2026-08-29 10:50 UTC (06:50 America/La_Paz).
  The separate 5212 token is expired and was not renewed by this incident repair.
  Renew tokens through source Settings/API and update the corresponding client secret.
