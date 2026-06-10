# SB015 Semantic Invariants

## Gate E Invariants
- Host beta verification must expose an async API with a `CancellationToken`.
- Pre-verification cancellation must stop verification before lane selection, orchestration, or audit append.
- Expected lane/payload preflight failures must return a structured denial result from the async API instead of throwing.
- Structured denials must include a typed denial code, message, lane, process run id, step run id, requester, mutation-denial flags, and an audit record with `DeniedCount = 1`.
- The existing sync `Verify` wrapper may remain for compatibility, but it cannot be the only host API shape.
- Lane selection remains exact. Unsupported or unregistered lanes are denied; there is no fallback to another lane.
- Payload selection remains strongly typed by lane-specific payload collections. No object/dynamic payload dispatch is allowed.
- Host beta changes must not add process state mutation, transition mutation, finalizer mutation, live OpenAI behavior, raw secret logging, or bundle-path coupling.

## Shallow-Pass Rejections
- Reject a proof package that only keeps the sync `Verify` method.
- Reject a proof package that handles missing payloads only through thrown `InvalidOperationException`.
- Reject a proof package that broadens lane dispatch with reflection, discovery, fallback selector behavior, `object`, or `dynamic`.
- Reject a proof package that does not include focused tests for async success, cancellation, unsupported-lane denial, and missing-payload denial.
- Reject a proof package that omits source assertions and anti-stub scans for the changed host files.

## Positive Proof Shape
- `IProcessVerificationRuntimeHost` exposes `VerifyAsync(ProcessVerificationHostRequest, CancellationToken)`.
- `ProcessVerificationRuntimeHost.VerifyAsync` validates cancellation, exact lane selection, typed lane payload presence, and successful orchestrator response count.
- `ProcessVerificationHostResult` separates successful responses from denied results.
- `ProcessVerificationHostDenialCode.UnsupportedLane` and `MissingLanePayload` are asserted by focused integration tests.
- The focused integration suite passed 19 `ProcessDomainEvidenceReadOnlyAdapterTests`.

## Gate Result
Gate E is semantically adequate for P05. The host beta API shape is async/cancellable, expected denials are structured, and no execution-capable or fallback behavior was introduced.
