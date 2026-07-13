# SB05 Proof Manifest

## Status

- Planned.

## Required Evidence

- Changed-file hashes.
- Build transcript.
- Workspace/input direct unit tests.
- Host-visible command smoke if command behavior changes.
- Boundary scan for workspace/input drivers outside `MafAgentRuntime`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Workspace tool functions | Workspace drivers/factory | Tool builder/runtime agent | Created per enabled workspace tools | Denied path/write tests |
| Prepared input attachments | Input attachment preparer | Runtime execution coordinator | Created per request | Request-scoped attachment scrub tests |
