# SB02 migration and model-snapshot review

Generated migration:
`src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260824224847_AddSharedProviderPersistence.cs`.

The generated `Up` operation creates all five Workspace-owned tables, their primary/alternate
keys, 13 lookup/uniqueness indexes, five restrictive foreign keys, the singleton check,
the public/internal identity check, and the invocation completion check. `Down` drops the five
tables in dependency-safe order. `AppDbContextModelSnapshot` contains the same mappings.

The real PostgreSQL clean-database test migrates from an empty database and asserts all five
tables. It also exercises actual unique-constraint rejection rather than inspecting metadata
only. The final EF pending-model command reports: `No changes have been made to the model since
the last migration.` The EF tool/runtime patch-version advisory is non-blocking and no model
drift exists.

Schema review found no raw token, Authorization header, URI userinfo/query/fragment, prompt,
request body, response body, image, attachment, tool argument, secret value, or encrypted content
column. `RemoteCatalogSnapshotJson` is a bounded cache of a validated, versioned, sanitized public
contract; it does not own identity.
