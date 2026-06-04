# Test Impact Inventory

Expected test areas:

- `ProcessRunAutomationDispatchServiceTests` artifact projection and lineage tests.
- Existing artifact regression filter: `FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Artifact`.
- Unit architecture tests guarding no premature core/driver projects.
- Source scans for MAF/Tooling product neutrality.
- New tests for source adapter key parity and duplicate skip behavior.
- New tests for write coordinator success/failure result mapping.

Codex must record exact test names in proof manifests before final closure.
