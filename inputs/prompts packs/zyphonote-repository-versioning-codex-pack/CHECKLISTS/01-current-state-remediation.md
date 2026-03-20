# Current state remediation checklist

- [x] Replace score disk storage dependence on `versionId` with repository blob/snapshot storage for new writes.
- [x] Replace playlist manifest storage dependence on `playlistVersionId` with repository blob/snapshot storage for new writes.
- [x] Keep learning package content-addressed manifest behavior as the pattern to emulate.
- [x] Add immutable event snapshot commits.
- [x] Add repository ids and current commit hashes to all four root entities.
- [x] Add commit hash bridge columns to existing legacy version tables.
- [x] Add read-model refresh logic when `main` moves.
