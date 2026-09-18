# CanDoItAll.Infrastructure

## Purpose

Infrastructure layer for EF Core context access, control-plane database profiles, storage, search, readiness, background queues, and health checks.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Infrastructure.csproj](CanDoItAll.Infrastructure.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Infrastructure owns persistence, storage, background runtime primitives, health, readiness, and control-plane concerns. Product rules should remain in modules and shared domain services.

## Search and Storage Persistence

`SearchDbContext` maps only `SearchDocument`. `StorageDbContext` maps only
`StorageCatalogRecord`, `StorageRoutingRule`, and retained `StoragePlacementIntentRecord`. Their runtime services use typed
factories bound to the immutable canonical database profile. Existing table names,
indexes, JSON payloads, and save-time concurrency stamping are retained; these
four entities currently have no concurrency-token properties. The complete
`AppDbContext` remains the schema/migration authority and the temporary maintenance
model. Owner contexts do not initialize or migrate production schemas.

Normal Search and Storage operations use independent contexts. The concrete owner
services also expose explicit `ForMutationAsync` methods for a caller that enters
an existing transaction with `CoordinatedDatabaseTransaction`. Those methods create
and dispose a fresh enlisted owner context, save their own changes, and leave
commit or rollback with the transaction owner. No ambient scope redirects the
normal factories. Project deletion stages Search and routing cleanup through these
owner methods in the Projects transaction. Prompt search projection callers and
project admission/replay fencing remain separate work.

Public catalog reads return immutable metadata snapshots, never mapped EF records or
raw provider/routing JSON. Explicit editor reads return `StorageProviderConfiguration`.
For `StorageCatalogSaveRequest`, omitted `Configuration` preserves the existing row's
exact owner-held bytes, including whitespace and unknown members; on a new row it
retains the historical `{}` default. Supplying typed configuration deliberately
replaces it using the existing validated serializer. `StorageRoutingRuleSaveRequest`
uses the same omission rule for alternatives, with `[]` for a new rule. The catalog
still owns identity creation, timestamps, validation and host binding; no new
concurrency/version contract is introduced.

Explicit driver inputs are detached from EF and retain original configuration privately
inside Storage. Only an operation reaching that selected driver/cache branch asks for
typed configuration. Metadata listing and unmatched routing rules do not parse it.
Storage computes the existing opaque Resources source and FileTools browse fingerprints
from the exact original bytes. Workspace editor default-purpose changes are owner
commands; disabling a route retains its existing alternative payload and other fields.

Storage catalog planning facts include every catalog row and retain selection and
host-binding fields. Callers supply the exact storage IDs whose configuration the
current planning phase would inspect, including any implicit/default selection.
FTP port/base-path configuration is parsed only for those IDs: malformed
referenced configuration fails, while unrelated malformed configuration stays
unparsed. The facts contain no raw configuration or credential reference. Fact
queries and staged routing cleanup do no bootstrap, path resolution, or provider
work. Existing normal catalog reads and writes retain their bootstrap and
host-path migration behavior.

StorageObjectDeletionService retains the selected current catalog record privately,
passes typed storage/bootstrap facts to one required caller validation callback, then
invokes the existing driver with a detached copy of the same selected row. The operation performs no database
write or bootstrap and contains no project-specific dependency. Its caller must hold
the managed-binding gate across the entire call. Project deletion invokes it only
after its authoritative database commit, under a separate postcommit Serializable
binding gate retained across final validation and the byte deletion. Existing path
containment, provenance, provider capabilities, and enabled/read-only checks remain
authoritative. This gate does not claim to serialize independent catalog edits.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Storage and host-path portability: `docs/architecture/storage-and-path-portability.md`

## Profile transfer sessions

The six selected transfer handlers receive `DatabaseTransferOperation`, containing
only resolved source/target profiles and the existing replacement choice. Canonical
contexts remain private to connection checks, schema maintenance, transaction and
lock orchestration. `DatabaseTransferOperationRunner` creates explicit source
Repeatable Read and target Serializable sessions for History, Providers/Security,
SimpleChats and workspace preferences. Projects retains its existing separate
Serializable source snapshot and final locked target operation. Ordinary factories
remain independent. Owner constructors are explicit typed delegates; no type registry
or service-provider contract selects foreign tables.

Session identities bind the exact selected profile, connection and transaction.
Mismatched, retired and replaced transactions fail explicitly. The two-InMemory
compatibility path is explicit and provides no relational atomicity guarantee;
mixed-provider destructive transfer and physical source/target aliases are rejected.

`StorageProfileTransferService` owns package catalog snapshots and staged byte access.
Its callback exposes typed references and results. Catalog/configuration records stay
inside Storage. Final catalog validation and pending bootstrap rows use the same
locked target transaction as the imported Projects/Workbench rows. Byte writes and
bounded compensation remain outside that target transaction.
