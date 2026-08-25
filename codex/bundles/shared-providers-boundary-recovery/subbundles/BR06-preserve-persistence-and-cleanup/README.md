# BR06 — Preserve persistence and clean up

## Objective

Finalize EF ownership without changing physical data, remove obsolete Workspace artifacts, and prove that existing databases remain compatible.

## Required implementation

1. Ensure ProviderManagement entity configurations are discovered through its module assembly marker.
2. Remove old Workspace EF configuration discovery only for moved provider/shared-provider types.
3. Preserve exact `ToTable`, keys, columns, indexes, lengths, concurrency tokens, foreign keys, and delete behaviors.
4. Keep existing migrations in history.
5. Run the repository's supported pending-model-change check without Docker.
6. When EF requires a new migration only because CLR type ownership changed:
   - generate it once
   - inspect generated operations
   - keep it only when both `Up` and `Down` are empty
   - document why it is metadata-only
7. Delete stale Workspace provider/shared-provider source, aliases, using directives, DI registrations, tests that assert wrong ownership, and user-facing text.
8. Remove temporary BR02 compatibility code now superseded by BR04.
9. Search for historical names and classify every remaining occurrence:
   - migration/table compatibility: allowed
   - original bundle historical docs: allowed until BR08 note
   - production ownership/naming: forbidden

## Hard failures

Any of the following blocks the subbundle:

- `RenameTable` for an existing provider/shared-provider table
- `DropTable` or `CreateTable` replacing an existing table
- data-copy SQL between old/new provider tables
- changed provider/shared-provider primary key
- changed secret ID/reference semantics
- new provider-specific DbContext

## Compatibility tests

Use non-container test facilities already present in the repository to prove:

- an existing personal provider row loads through ProviderManagement
- existing publication/source/import/invocation rows materialize
- foreign keys and delete behavior match prior model
- existing provider IDs remain stable
- existing secret references resolve without plaintext migration
- provider transfer remains compatible

Where a real PostgreSQL lifecycle test is Docker-only, perform model/migration inspection and defer the lifecycle command explicitly to original SB07 rather than running Docker.

## Acceptance

- EF model has no pending functional schema operation.
- Physical table names are unchanged.
- Workspace production source contains no provider/shared-provider ownership residue.
- No temporary compatibility implementation remains.
- Affected migration, Infrastructure, ProviderManagement, Workspace, and host projects build.

## Commit

`BR06: preserve provider schema and remove workspace residue`
