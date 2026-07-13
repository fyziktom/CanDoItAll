# SB36 Semantic Invariants

## SB36-INV-01 - Selection Is Explicit And Fail-closed

- Raw note: provider registry order must never grant agent memory access.
- Expected: only an explicit provider, assignment, or fallback-permitted named default inside the allowed set may dispatch.
- Disallowed shallow implementation: a deny enum that still returns the first compatible provider.
- Failing-first: `bundle://proof/SB36/transcripts/failing-first-evidence.txt`.
- Passing evidence: `bundle://proof/SB36/transcripts/reported-validation.txt`.
- Source hashes: `bundle://proof/SB36/transcripts/file-hashes.txt`.
- Red-team negative: unassigned/default/disallowed candidates produce no authorized dispatch.
- Downstream check: SB37 immutable binding plans name provider IDs explicitly.

## SB36-INV-02 - Operation Identity Is Authority-bound

- Raw note: possession of an operation GUID must not disclose or cancel another agent/session/workflow operation.
- Expected: complete persisted owner context is matched before status/cancel.
- Disallowed shallow implementation: provider ID or request GUID alone is sufficient.
- Failing-first: foreign requester in `bundle://proof/SB36/transcripts/failing-first-evidence.txt`.
- Passing evidence: authorizer outcome in `bundle://proof/SB36/transcripts/reported-validation.txt`.
- Source hashes: `bundle://proof/SB36/transcripts/file-hashes.txt`.
- Red-team negative: different requester/agent/session/workflow/process is denied without details.
- Downstream check: SB40 reran terminal ownership negatives and the aggregate passed.

## Validator Invariant Contract

- Invariant ID: SB36-SELECTION-OWNERSHIP
- Source raw note: provider selection must respect explicit agent choice and operation status/mutation must remain owner-authorized.
- Expected behavior: deny fallback dispatches nothing without a named choice; exact requester/agent/session/workflow/process ownership precedes disclosure or mutation.
- Disallowed shallow implementation: unused deny metadata, first-provider selection, or GUID-only operation authority.
- Failing-first test: bundle://proof/SB36/transcripts/failing-first-evidence.txt.
- Passing test: bundle://proof/SB36/transcripts/reported-validation.txt and bundle://proof/SB40/transcripts/terminal-validation.txt.
- Changed source files: repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderRegistry.cs and repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationAccessAuthorizer.cs.
- Production assertions: explicit policy is evaluated before driver resolution and persisted complete ownership is checked before lifecycle access.
- Red-team negative case: unassigned provider and foreign requester cases must remain zero-dispatch/denied.
- Downstream dependency check: SB37 fan-out consumes only immutable authorized selection plans; SB40 rechecked the aggregate.

## SB36-INV-03 - Application Ownership Is Real

- Expected: non-partial facade plus cohesive top-level services; Application depends only on provider-neutral contracts.
- Disallowed shallow implementation: rename partial files or move the same god class unchanged.
- Source/anti-stub proof: `bundle://proof/SB36/transcripts/source-and-anti-stub-audit.txt`.
- Hash proof: `bundle://proof/SB36/transcripts/file-hashes.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Selection result | registry/evaluator hashes | handler/query service | operation creation | deny-fallback characterization |
| Owner record | operation coordinator/handler | access authorizer/status service | status/cancel | foreign-owner characterization |
