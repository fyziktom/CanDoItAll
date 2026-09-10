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
`StorageCatalogRecord` and `StorageRoutingRule`. Their runtime services use typed
factories bound to the immutable canonical database profile. Existing table names,
indexes, JSON payloads, and save-time concurrency stamping are retained; these
three entities currently have no concurrency-token properties. The complete
`AppDbContext` remains the schema/migration authority and the temporary maintenance
model. Owner contexts do not initialize or migrate production schemas.

Normal Search and Storage operations use independent contexts. The concrete owner
services also expose explicit `ForMutationAsync` methods for a caller that enters
an existing transaction with `CoordinatedDatabaseTransaction`. Those methods create
and dispose a fresh enlisted owner context, save their own changes, and leave
commit or rollback with the transaction owner. No ambient scope redirects the
normal factories. This prerequisite does not yet convert project deletion or
Prompt search projection callers, or establish project admission/replay fencing.

Storage catalog planning facts include every catalog row and retain selection and
host-binding fields. Callers supply the exact storage IDs whose configuration the
current planning phase would inspect, including any implicit/default selection.
FTP port/base-path configuration is parsed only for those IDs: malformed
referenced configuration fails, while unrelated malformed configuration stays
unparsed. The facts contain no raw configuration or credential reference. Fact
queries and staged routing cleanup do no bootstrap, path resolution, or provider
work. Existing normal catalog reads and writes retain their bootstrap and
host-path migration behavior. Physical byte operations remain outside database
transactions and under the existing containment/provenance policies.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Storage and host-path portability: `docs/architecture/storage-and-path-portability.md`
