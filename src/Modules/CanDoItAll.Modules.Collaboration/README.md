# CanDoItAll.Modules.Collaboration

## Purpose

Application owner and routed production host for `/collaboration` and `?threadId=<guid>`.
The complete workspace renders through `CanDoItAll.Collaboration.UI`; plain models and enums
live in `CanDoItAll.Modules.Collaboration.Contracts` with their existing CLR namespaces.

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

`CollaborationWorkspaceSession` is owned by one routed page. It owns desired selection,
accepted aggregate reads, draft/EditContext lifetimes, command admission and reconciliation.
Its owner port resolves the same scoped `CollaborationService` that the shell observes.
The renderer never calls persistence or performs navigation. Same-target refresh preserves
drafts; switching targets discards the reply; quick-create stays independent. A saving form
is disabled. A saved write followed by a failed read remains saved with a read-only retry.

Changed observers are isolated and logged individually after the owner operation, so one
observer cannot conceal a successful save or prevent other observers from being notified.
Shell refresh faults retain the accepted badge count and are logged. Context navigation is
restricted to local application routes; message content remains escaped text.

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

- [Collaboration UI boundary and proof](../../../docs/architecture/collaboration-ui-boundary.md)
- [Real UI sandbox](../../Sandboxes/CanDoItAll.Collaboration.UiSandbox/README.md)

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
