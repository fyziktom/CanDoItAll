# Enterprise chatbot readiness

The bundle implements an interactive/API simple-chat product. It does not implement a public chatbot
deployment. The current model must nevertheless avoid a future transcript migration.

## Supported now

- reusable and suspendable definition;
- immutable definition revision;
- durable conversation and operation;
- origin kind (`Application` or `Api`);
- stable operation and turn IDs;
- profile/organization scope selected from canonical repository identity;
- provider/model/settings snapshot;
- provider/model-specific thinking-effort override and invocation evidence;
- usage and failure audit;
- API transport.

## Prepared extension seams

### Deployment aggregate

Future `LlmChatDeployment` should bind:

- one definition revision;
- channel/transport adapter;
- ingress authentication mode;
- rate limits and quotas;
- moderation/input/output policies;
- retention;
- branding;
- tenant/data-residency and retention policy;
- external participant identity mapping;
- optional human-handoff policy;
- rollout state that pins an immutable definition revision.

It is a separate aggregate. Deployment state must not be stored on the reusable definition itself.

### Conversation origin

The current typed origin supports `Application` and `Api`. A later deployment bundle adds
`ExternalChannel` plus a separate opaque deployment/source association. This changes product metadata,
not transcript identity or message rows. The current Web API rejects unknown/external origins.

### Policy contributors

A later deployment module may introduce narrow input/output/access policy ports when real consumers
exist. This bundle must not add unused policy interfaces or a generic middleware engine. When a later
bundle has at least three real policy variants, select an ordered pipeline at that time.

### Participant identity

Do not map anonymous visitors into the local user directory. A later deployment module owns opaque
external participant references and privacy/retention policy.

## Explicitly deferred

- web widget;
- Teams/Slack/WhatsApp/voice adapters;
- anonymous visitor cookies;
- external OAuth;
- moderation providers;
- PII detection/redaction;
- retrieval/RAG;
- human handoff;
- streaming/SSE/WebSocket transport;
- multi-instance background dispatcher;
- rate limiting and billing plans;
- deployment analytics;
- enterprise SSO/SCIM and external subject mapping;
- eDiscovery/export and legal-hold policy;
- regional data residency and channel-specific deletion workflows.

CP1 and CP2 must verify that none of these were partially smuggled into the current product model.
