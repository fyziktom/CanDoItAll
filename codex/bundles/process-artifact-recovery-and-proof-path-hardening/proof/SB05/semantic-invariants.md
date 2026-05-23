# SB05 Semantic Invariants

## Invariants

- Invariant ID: `SB05-I001`
- Source raw note: User required backup of actual project-structure data and API-only data loading before rerun.
- Expected behavior: Existing project structure and assets are backed up under the requested output root before running the demo.
- Disallowed shallow implementation: Direct database mutation or hidden test seeding.
- Failing-first test: Earlier duplicate process imports made the demo DB confusing; zero-run duplicates had to be removed through the process API.
- Passing test: `bundle://proof/SB05/transcripts/api-backup-and-seed.txt` records API backup, template presence, duplicate cleanup, and Cognitive Memory disabled.
- Changed source files: None for backup; data records were changed through HTTP APIs.
- Production assertions: `backup-summary.json` records project id, target node id, output root, product root, evidence root, backup root, node count, and asset count.
- Red-team negative case: Deleting definitions with completed run history was rejected; only zero-run duplicates were removed.
- Downstream dependency check: SB06 launched from API-backed project/process records and wrote outputs under the requested folder.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Backup manifest | Project-structure API export | Operator and rerun process | Durable file under bundle proof and requested output root | `bundle://proof/SB05/backups/backup-summary.json` |
| Clean template set | Process definition API | Process launch selection | Keeps latest Blazor templates while preserving historical run definitions | `bundle://proof/SB05/transcripts/api-backup-and-seed.txt` |
