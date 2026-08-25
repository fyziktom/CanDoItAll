# SB04 August 25 security revalidation

Independent result: `PASS`. No security blocker remains in the reopened SB04 delta.

## Retention and request containment

- Responses accepts `store` only when its JSON kind is exactly `false`. `true`, `null`, strings,
  duplicate properties, and persistence/server-state fields such as `background`, `conversation`,
  and `include` fail before dispatch.
- Omission no longer delegates retention semantics to an upstream default. Canonicalization writes
  `store: false`, so every accepted Responses request carries an explicit non-retention instruction
  across the connector boundary.
- The canonicalizer copies only the already allowlisted, bounded, duplicate-free root object and
  adds the one constant field. It introduces no caller-controlled URI, header, credential, or
  routing escape.

## Operation and capability containment

- After resolving the opaque public model identity against current persisted state, Workspace
  requires both adapter support for the requested operation and the matching typed model
  capability (`ChatCompletions`, `Responses`, or `ImageGenerations`). Unknown enum values fail the
  same check.
- A mismatch returns the sanitized conflict code `shared_provider_operation_mismatch` before
  target creation, invocation audit start, or dispatcher use. The error has no parameter and does
  not reflect the public model ID, upstream model, private URI, credential, or provider details.
- Structured-output persistence is fail-closed: connector defaults are an upper bound, an existing
  profile can persist `false`, catalog projection then omits the capability, and structured requests
  are rejected before the dispatcher. An editor cannot advertise the feature for a connector whose
  typed defaults do not support it.

## Cancellation-fixture containment

- `HoldAfterFirstFrame` is available only in the deterministic test-host control schema. The
  `/_test` routes require the distinct control bearer token; ordinary fixture data credentials do
  not authorize mutation.
- The fixture permits the mode only for Chat Completions or Responses and rejects combinations with
  injected failure modes. Non-stream requests reject it explicitly.
- Each request snapshots control state once, flushes exactly one nonterminal frame, then waits on
  `RequestAborted` with a hard 60-second ceiling. Cancellation is swallowed only when that request's
  abort token is signaled; no synthetic `[DONE]` is emitted from the hold path.
- The mode stores no prompt, response, credential, or access-context data and adds no production
  endpoint, service registration, or runtime fallback.

## Evidence assessment

- Exact request-policy coverage discovers 24 cases and passes 24/24:
  `../transcripts/sb04-reopen-list-relay-policy-release.txt` and
  `../transcripts/sb04-reopen-run-relay-policy-release.txt`.
- Exact compatibility coverage discovers 22 cases and passes 22/22, including sanitized
  operation/purpose mismatch and structured-output denial:
  `../transcripts/sb04-reopen-list-openai-compatibility-release.txt` and
  `../transcripts/sb04-reopen-run-openai-compatibility-release.txt`.
- Exact streaming coverage discovers 12 cases and passes 12/12, including downstream cancellation,
  upstream disposal, timeout, malformed-frame, and no-synthetic-success behavior:
  `../transcripts/sb04-reopen-list-streaming-release.txt` and
  `../transcripts/sb04-reopen-run-streaming-release.txt`.
- The owning Unit and Integration Release builds have zero warnings and zero errors:
  `../transcripts/sb04-reopen-build-unit-release-final.txt` and
  `../transcripts/sb04-reopen-build-integration-release-final.txt`.
- Persisted-route assertions additionally verify explicit/omitted `store: false`, zero dispatch on
  operation mismatch, generic error content, and persisted structured-output opt-out in the 10/10
  backend checkpoint:
  `../../../SB07-backend-checkpoint-three-instance-proof/proof/transcripts/38-focused-test-release-final.txt`.

The pending SB07 multi-instance lifecycle is not represented as green by this review. It owns the
live container-to-container cancellation observation; the SB04 result is limited to the reviewed
product contract, deterministic fixture containment, and the fresh governed focused evidence.
