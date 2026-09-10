# CanDoItAll.Modules.Collaboration

## Purpose

Product module for collaboration concepts and UI/runtime surfaces.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Collaboration.csproj](CanDoItAll.Modules.Collaboration.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

`CollaborationService` reads and writes through the bounded `CollaborationDbContext`,
which maps only threads, participants, messages, and inbox items. Its per-operation
factory uses the host's immutable canonical database profile. GUID token stamping,
activity mirroring, notifications, and existing table/identifier shapes are retained.
The current mappings do not enforce optimistic concurrency on those tokens.

The complete application migration model reuses the same four mapping configurations;
the runtime context does not create or migrate the database. The transfer-residue
participant still reads under the infrastructure-owned target transaction through the
legacy maintenance contract pending the coordinated transfer-boundary change. It is
not an alternative business writer.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
