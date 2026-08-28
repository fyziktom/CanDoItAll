# SB08: Real multi-agent thinking validation

## Status

- Status: Completed
- Proof tier: Governed

## Objective

Close N011/R11 and N012/R12 with real source/client proof and preserved Docker data.

## Covered Inputs

- inputs/06-thinking-effort-feedback.md.

## Prerequisites

- SB07 focused tests/build pass. Actual source OpenAI and Ollama remain reachable.

## Exact Source References

- repo://src/App/CanDoItAll.Web/Dockerfile

## Deliverables

- Rebuild 5210/5212/5214 preserving all databases, volumes and user setup. Configure
dedicated test agents through Playwright MCP on 5212 and synchronize the source via UI.
Run same-model/different-effort, different-model/same-effort and source-default cases.
Verify outgoing effort metadata and real upstream success/source usage, not model
self-report or token-count heuristics. Confirm unsupported model selection disables
effort and forged invalid overrides are rejected before upstream dispatch.

## Dependency Impact

- Depends on SB07. Do not touch 5032, reset 5214, change upstream URLs to fixtures,
overwrite user agents or expose credentials. Low bounded prompts limit test cost.

## Validation Depth

- Governed. Targeted Playwright MCP UI flows plus sanitized outgoing request metadata,
source invocation records and health. Tests are named in the live matrix before runs.
No additional unfiltered suite; SB07 owns any impact-required broad gate.

## Acceptance Checklist

- Model names/order/effort choices match source support.
- Same/different agent settings survive save/reload and reach actual upstreams independently.
- Real OpenAI and Ollama responses and source usage are recorded.
- All three containers healthy with user data preserved; no secrets in proof.

## UI Composition

1920x1080; existing Runtime tab, native model/effort dropdowns; dialog body scroll.
Inspect normal/open lists, readable options, visible Save, no extra layout changes.

## Proof Required

- proof/SB08 manifest, semantic invariants, inspected screenshots, sanitized request
metadata correlated with source invocations, build/restart/health transcripts.

## Progression Gate

- Final closure only after real successful cases and meaningful negative proof. Report
external provider limitations as blocked cases rather than substituting fixtures.

Final gate: Pass for the nine-request real Responses/Ollama matrix and exact source/client
model/effort parity. Seven agents configured through Playwright MCP, all source records
Succeeded/Complete. OpenAI Chat Completions reasoning/tool combinations remain explicitly
unsupported where upstream rejects them; the shared Responses profile supplies the
validated path without changing the user's original transport. All three hosts healthy,
existing data retained. See proof/SB08/manifest.md.
