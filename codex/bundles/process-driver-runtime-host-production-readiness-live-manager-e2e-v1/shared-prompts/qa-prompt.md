# QA / Red-Team Prompt

Reject the implementation if any of the following are true:

- `EfCoreProcessVerificationAuditStore` exists but app DI still resolves `InMemoryProcessVerificationAuditStore` for production process runtime.
- Expected verification-host request validation still reaches manager/API as exceptions rather than structured denials.
- Runtime host can call shell, file system, network, Office/Graph, workspace/storage writes, process mutation, claims, transitions, finalizers, or retries.
- Live proof was skipped but reported as passed.
- UI screenshots are attached but not reviewed against run id, audit id/hash, denial category, and no-mutation fields.
- Any source/test depends on concrete `codex/bundles/<name>` paths.
