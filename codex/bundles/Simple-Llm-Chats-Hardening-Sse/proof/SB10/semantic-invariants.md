# SB10 semantic invariant contract

Changed-file hashes: `bundle://proof/SB10/changed-files.sha256`.
Positive commands: `bundle://proof/SB10/transcripts/01-current-head-gates.md`.
Negative/source commands: `bundle://proof/SB10/transcripts/02-negative-and-source-guards.md`.

## SBI-10-01 — provenance is server-owned

- Expected behavior: HTTP clients can submit a title but cannot submit trusted origin; Web always
  creates `Api`, while trusted application commands can explicitly create `Application`.
- Disallowed shallow implementation: default a still-bindable origin field, accept unknown provenance,
  rewrite only the response, or leave a second HTTP path accepting origin.
- Passing proof: spoofed JSON returns 400 before the application service runs; a real PostgreSQL row
  created without origin stores `Api`; direct product proof preserves `Application`.

## SBI-10-02 — read, manage, and execute are exact capabilities

- Expected behavior: authorization-enabled hosts require the exact dedicated scope for each route;
  authorization-disabled trusted-local hosts retain their established behavior.
- Disallowed shallow implementation: authenticate only at `/api`, let broad `api` imply all LLM Chat
  rights, define claims without endpoint metadata, or protect SSE differently from status reads.
- Passing proof: broad `api` is 403; read can list/status/events but cannot mutate/execute; manage can
  create but not read/execute; execute can admit/cancel but not read/manage. Local no-token calls pass.

## SBI-10-03 — normal bearer transport only

- Expected behavior: SSE uses the standard Authorization header and the same read policy as status.
- Disallowed shallow implementation: `access_token`, API key, or credential query parameters; anonymous
  stream preflight; browser-only cookie/circuit assumptions.
- Passing proof: a read-scope Authorization header reaches operation lookup (404 for an unknown ID),
  while the same JWT in `access_token` is rejected with 401 before the endpoint runs.

## SBI-10-04 — versioned, redacted external contract

- Expected behavior: operation snapshots and events are versioned stable transport envelopes; failure
  conflicts expose only product codes, safe identity, and retry disposition; definition reads omit the
  stored system prompt.
- Disallowed shallow implementation: serialize domain/EF entities, raw provider frames/exceptions,
  prompt/system content, credentials, SQL text, or both conflicting fingerprints/request bodies.
- Passing proof: runtime/OpenAPI assertions find `schema`, transport DTO names and canonical links;
  response assertions exclude system prompt and conflicting messages; source guard finds no exception
  object passed to any LLM Chat logger.

## SBI-10-05 — future deployment remains a separate boundary

- Expected behavior: participant identity, channels, anonymous policy, moderation, quotas, retention,
  residency, legal hold, and handoff belong to a later `LlmChatDeployment` aggregate/adapter.
- Disallowed shallow implementation: dormant nullable fields/tables on definitions or ordinary internal
  conversations, wildcard CORS, anonymous widget, or a globally scoped conversation idempotency key.
- Passing proof: source guard finds no deployment/participant markers and no schema change. The ADR
  records why conversation-create idempotency waits for a deployment-scoped identity namespace.

## Anti-stub result

The scoped production audit finds no partial extraction, anonymous/public route, query bearer token,
dormant deployment model, raw exception logger, or second origin binder. Real hosts exercise endpoint
metadata, JWT validation, application commands, EF persistence, OpenAPI, and the existing SSE session.
