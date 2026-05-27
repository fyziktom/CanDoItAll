# MAF 1.6 Processes Final Preflight Hardening v4

## Status

Prepared for Codex execution.

## Branch context

- Repository: `fyziktom/CanDoItAll`
- Reviewed branch: `processes-hardening`
- Reviewed head: `phase10` / `6b7cb12597718d1229cee8e4a6dc1f7c0fd34c16`

## Summary of review

The previous bundle was completed and the implementation is better:

- MAF package references are on the 1.6 line.
- A reflection test exists for loaded MAF/A2A assemblies and key symbols.
- Context injection uses `MessageAIContextProvider`.
- MAF session serialization/restoration and response format handling are used.
- `RecordArtifactAsync` now rejects projection identity / external reference reuse across different step or expectation scopes.
- Required narrative artifacts can report `ContentUnavailable`.

However, this is not yet the point where a full real UI process test should be run without another preflight pass.

## Most important remaining issue

The read model currently handles `ContentUnavailable` diagnostics, but it does not appear to downgrade recorded artifacts for all finalizer validation statuses.

A recorded artifact with a finalizer diagnostic like:

- `StaleOrWrongRun`
- `WrongProducerMode`
- `ContentHashMismatch`
- `InvalidFormat`
- `PlaceholderOnly`
- `InsufficientEvidence`

could still be displayed as `Satisfied` or `AutoProjected` unless the read-model parity logic is expanded. This is dangerous because the UI/operator may see a green artifact even though the finalizer would reject it.

## Goal of this bundle

Close the final proof gaps before real testing:

1. Distinguish real MAF 1.6 adoption from package compatibility.
2. Prove actual runtime behavior through tool-loop/context/finalizer/session/handoff/workflow tests.
3. Expand artifact validation read-model parity across all statuses.
4. Add a controlled step0 live smoke harness.
5. Produce an explicit go/no-go report for the next real UI test.
