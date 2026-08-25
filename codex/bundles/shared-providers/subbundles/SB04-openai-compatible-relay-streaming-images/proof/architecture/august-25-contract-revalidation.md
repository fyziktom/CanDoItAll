# SB04 August 25 contract revalidation

Independent result: `PASS`. No architecture blocker remains in the reopened SB04 delta.

## Reviewed delta

- Responses retention policy now accepts only the exact JSON value `store: false` and injects
  `store: false` into the canonical upstream request when the caller omits the field.
- persisted relay dispatch now checks both the adapter operation set and the selected model's
  operation capability before target creation, audit start, or dispatcher invocation;
- the generic Workspace editor can narrow structured-output support on an existing profile while
  connector defaults remain an upper bound and the established create default is preserved; and
- the deterministic upstream fixture has a typed, surface-scoped `HoldAfterFirstFrame` mode for
  proving downstream cancellation after a real nonterminal SSE frame.

## Boundary and dependency findings

- `SharedProviderRelayRequestPolicy` retains normalization and wire-policy ownership in
  `CanDoItAll.SharedProviders.Http`. The added property name and canonicalization branch are
  private implementation details; no protocol or product dependency entered Http.
- `SharedProviderRelayApplicationService` retains current-state and capability-coherence ownership
  in Workspace. Its new operation check is private and consumes existing Abstractions enums and
  persisted projections; it does not move routing, persistence, or secrets into Web or Http.
- `WorkspaceService.SaveProviderAsync` changes only persistence semantics on the existing editor
  contract. The connector capability default is an upper bound, so an unsupported connector cannot
  be widened by an editor value. The asymmetric new-profile rule is an intentional compatibility
  constraint: it preserves the established connector default on first save, while later saves can
  explicitly narrow structured output.
- `FixtureStreamMode` and the added request/snapshot member are public only in the non-packable
  deterministic test-host executable. They are a typed HTTP control schema, not a production relay
  surface, and create no production `ProjectReference`.
- No project file or project-reference change is required by this delta. The existing dependency
  direction remains Web -> Workspace/Abstractions, Workspace -> Abstractions, and Http ->
  Abstractions; the test fixture remains standalone.

## Public surface, partial-class, and cohesion review

- No production public type, member, constructor, record arity, or serialization contract was
  added or changed.
- The only additive public declarations are the test-only `FixtureStreamMode` enum and its typed
  members on `TestControlRequest`/`TestControlSnapshot`. Invalid enum values, unsupported surfaces,
  and failure-mode combinations are rejected by the fixture control endpoint.
- No partial class was introduced or extended. The request-policy, Workspace application-service,
  editor persistence, and fixture streaming responsibilities remain in cohesive existing types.
- The fixture captures one immutable control snapshot per OpenAI request. This prevents a control
  mutation from changing failure/stream behavior halfway through a request. Its hold is bounded by
  60 seconds and observes `HttpContext.RequestAborted`; it cannot become an unbounded production
  wait path.

## Evidence assessment

- Unit Release build: zero warnings and zero errors in
  `../transcripts/sb04-reopen-build-unit-release-final.txt`.
- Exact relay-policy discovery and execution: 24 discovered and 24/24 passed in
  `../transcripts/sb04-reopen-list-relay-policy-release.txt` and
  `../transcripts/sb04-reopen-run-relay-policy-release.txt`.
- Integration Release build: zero warnings and zero errors in
  `../transcripts/sb04-reopen-build-integration-release-final.txt`.
- Exact compatibility discovery and execution: 22 discovered and 22/22 passed in
  `../transcripts/sb04-reopen-list-openai-compatibility-release.txt` and
  `../transcripts/sb04-reopen-run-openai-compatibility-release.txt`.
- Exact streaming discovery and execution: 12 discovered and 12/12 passed in
  `../transcripts/sb04-reopen-list-streaming-release.txt` and
  `../transcripts/sb04-reopen-run-streaming-release.txt`.
- The later backend checkpoint additionally exercises the persisted structured-output opt-out,
  canonical `store: false`, and both Responses/image-to-Chat operation mismatches: 10/10 passed in
  `../../../SB07-backend-checkpoint-three-instance-proof/proof/transcripts/38-focused-test-release-final.txt`.

The deterministic hold mode is reviewed here as bounded test infrastructure. This review does not
claim the still separately governed SB07 Docker lifecycle result; that remains an SB07 proof
boundary and does not invalidate the focused SB04 contract result.
