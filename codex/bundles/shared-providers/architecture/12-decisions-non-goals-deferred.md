# Decisions, non-goals, and deferred work

## In scope v1

- central explicit publication;
- sanitized catalog;
- OpenAI-compatible models, Responses, Chat Completions, streaming, function tools,
  structured output, tested vision, and images;
- OpenAI/Ollama/ComfyUI connectors currently configurable in Workspace;
- source/import synchronization;
- hybrid personal/shared providers;
- access-context reference and usage/audit preparation;
- desktop UI;
- three-app Docker proof;
- documentation, OpenAPI, SharedInfo.

## Conditional scope

Azure OpenAI is included only if SB00 proves a current Workspace-configurable production
connector and the implementation can add a relay adapter without distorting the bundle. An MAF
driver alone does not justify advertising a central publication.

Vision support is included per publication only when current local runtime and relay tests
prove the exact content forms and size policy.

Provider model pricing may be included in catalog only when it is intentionally public,
sanitized, and contractually stable. Otherwise client imported pricing remains unknown and
central usage owns cost.

## Explicit non-goals

- full EGCP implementation;
- user/group/role policy engine;
- routing or quotas by identity;
- central administration through a public management API;
- sharing of provider API-key values;
- arbitrary transparent HTTP proxy;
- provider-side built-in tools;
- provider file management;
- audio endpoints without current production drivers;
- batch API;
- fine-tuning/model maintenance through shared API;
- automatic provider failover;
- silent fallback to a personal key;
- mobile-specific CanDoItAll UI;
- replacing existing simple chat/agent APIs;
- moving providers out of Workspace in this feature;
- SQLite product persistence;
- real paid provider calls in automated proof.

## Deferred follow-ups

- EGCP authorization/routing/policy UI;
- quotas, budgets, per-tenant rate limits;
- signed access-context tokens or lookup service;
- public pricing policy and chargeback;
- audio relay when production STT/TTS drivers exist;
- provider-specific advanced features behind explicit policies;
- multi-central source priority/failover;
- automatic background catalog refresh scheduling beyond safe manual/on-demand and simple
  bounded refresh;
- centralized revocation push/event channel;
- distributed rate limiting across multiple central replicas;
- long-term audit export/retention configuration UI.

Deferred items must not appear as placeholders that weaken v1 behavior.
