# Scope and outcomes

## Primary outcome

A central CanDoItAll installation can explicitly publish selected AI provider profiles.
Independent CanDoItAll installations can register that central installation as a shared
provider source, discover a sanitized catalog, select publications, and use them through a
shared connector without receiving upstream credentials.

## Required user journeys

### Central administrator

1. Configure ordinary OpenAI, Ollama, ComfyUI, or future provider profiles as today.
2. See whether each profile is eligible for sharing and why.
3. Explicitly publish or unpublish an eligible profile.
4. Issue an API token with catalog and invocation scopes.
5. Observe health, use, errors, and usage attribution without content logging.

### Client administrator or user

1. Keep existing personal provider profiles.
2. Add a shared-provider source with display name, central base URI, and one secret reference.
3. Test source connectivity and authorization.
4. Load the remote catalog.
5. Select one or more publications and confirm.
6. Receive stable local provider profiles for the selected publications.
7. Use shared and personal providers side by side in agents, simple chats, workflows, image
   generation, and other existing provider consumers supported by the advertised capability.
8. Synchronize later without duplicating profiles or breaking local references.
9. Understand unavailable, removed, changed, unauthorized, and source-offline states.

### Future EGCP

1. Route the same catalog and inference surface through an intermediate gateway.
2. Attach an opaque access-context reference without expanding every API DTO.
3. Correlate authenticated caller, access context, provider publication, model, usage, cost,
   outcome, and trace.
4. Replace or enrich the referenced context outside the shared-provider DTO contract.

## Backend-first outcome

Before any new UI is implemented, the backend must be proven through:

- real PostgreSQL persistence and migrations;
- real API authorization;
- real HTTP catalog and compatibility endpoints;
- non-streaming and streaming behavior;
- source synchronization and idempotent reconciliation;
- local runtime invocation through the shared connector;
- one central and two client CanDoItAll application containers;
- a deterministic external upstream boundary;
- negative security and outage scenarios.

## Delivery outcome

The final implementation includes product documentation, repeatable Docker tooling, current
OpenAPI, a SharedInfo API skill, validation evidence, and a running three-instance stack for
operator testing.
