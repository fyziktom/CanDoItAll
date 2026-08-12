# Runtime macOS actual-host validation deferral

## Decision

The operator explicitly authorized runtime implementation to continue on the current branch without an available actual macOS host. This record applies `RUNTIME-MACOS-VALIDATION-001` to B01–B07.

## Permitted progression

B01–B06 implementation gates may close for branch progression when all of the following are true:

- focused and affected Windows validation is green;
- the same relevant behavior is green on actual Linux;
- macOS-specific selection, command, path, permission, unavailability, and failure behavior has deterministic contract/fixture coverage;
- unproved native or desktop capabilities remain explicitly `Unavailable` or `Unsupported` and fail closed;
- no documentation, diagnostic, manifest, or UI claims actual macOS support;
- no locally reproducible product, security, architecture, migration, data-preservation, process-ownership, or redaction defect is waived.

This is a validation-host deferral only. It does not authorize fake platform results, mocked tests presented as actual-host evidence, automatic elevation, insecure fallback, or weakened policies.

## Still deferred

- actual macOS process-tree and cancellation behavior;
- macOS executable and filesystem semantics on configured filesystems;
- macOS Manager recovery discovery;
- macOS terminal and desktop FileTools behavior;
- native Keychain and other macOS host integrations;
- hosted macOS CI artifacts and final support-matrix claims.

## Gate effect

- R1a through R3 may record `GO for implementation under RUNTIME-MACOS-VALIDATION-001` when their Windows/Linux and deterministic contract proof is otherwise complete.
- B07 may finish branch-local implementation and prepare the final candidate, but R4 remains `DEFERRED`, not `GO`, until actual macOS and the separately deferred hosted proof are supplied.
- Any actual macOS failure later reopens the owning subbundle and downstream gates.
