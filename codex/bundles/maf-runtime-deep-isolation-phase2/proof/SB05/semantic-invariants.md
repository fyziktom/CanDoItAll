# SB05 Semantic Invariants

## Invariants

- Workspace access policy must not be broadened by extraction.
- Input attachments must remain scrubbed from persisted sessions when request-scoped.

## Shallow-Pass Trap

- Wrapping all workspace methods in one new class without separating policy and drivers would compile but preserve the same testability problem.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Workspace tool functions | Workspace drivers/factory | Tool builder/runtime agent | Created per enabled workspace tools | Denied path/write tests |
| Prepared input attachments | Input attachment preparer | Runtime execution coordinator | Created per request | Request-scoped attachment scrub tests |
