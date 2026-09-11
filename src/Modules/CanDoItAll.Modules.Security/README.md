# CanDoItAll.Modules.Security

## Purpose

Product module for app security surfaces and security-related runtime services.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Security.csproj](CanDoItAll.Modules.Security.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

`SecurityDbContext` contains exactly the existing `SecretRecord` and `SecretReference` mappings. The complete application model remains the migration authority; table names, columns, indexes, GUID identities, and legacy payload formats are unchanged. These entities have no concurrency-token fields or application-managed stamping, and the owner context does not add them.

Runtime secret services, resolution, and material-migration coordinators use a pooled factory bound to the immutable `ICanonicalRuntimeDatabase.Profile`. Ordinary factory calls remain independent. `SecretReferenceQuery.GetExistingIdsAsync` returns existing secret IDs without names or payloads. `ExistsForMutationAsync` performs the existence read inside the caller's explicitly entered coordinated transaction.

Deletion retains its serializable mutation scope and advisory keys. Reference policies accept a secret ID and enlist their own context through `CoordinatedDatabaseTransaction`; their reads remain part of the deletion transaction. After database commit, the coordination frame is released before vault cleanup and activity callbacks. Vault replacement, cleanup failure behavior, legacy protection, migration checkpoints, and current runtime authorization rules are preserved. Purpose-policy refinements remain a separate task.

`SecurityOwnerPersistenceTests` covers exact model parity, the absence of token stamping, legacy record/binding readback after restart and owner edit, profile isolation, independent/enlisted reference reads, and serializable deletion with post-commit vault callbacks. Existing secret vault, runtime authorization, migration, portability, and cross-module deletion tests remain required. InMemory fixture cases do not prove PostgreSQL transaction atomicity.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`

Selected Provider transfer uses `ISecretDatabaseTransferParticipant`: callers supply only
an active transfer handle and selected secret IDs. Security reads and stages its own
records on the exact source snapshot and target transaction supplied by Infrastructure.
Encrypted payloads and all saved metadata remain inside the Security owner; no vault,
provider, or file operation runs in the database transaction. Existing references and
unselected secrets remain intact. Missing selected source secrets retain the shipped
replacement behavior. PostgreSQL intermediate saves roll back with the Provider stage;
the explicit two-InMemory test path does not claim transaction atomicity.
