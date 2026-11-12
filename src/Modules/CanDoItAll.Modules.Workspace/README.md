# CanDoItAll.Modules.Workspace

## Purpose

Product module for workspace records, workspace state, and cross-module workspace services.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Workspace.csproj](CanDoItAll.Modules.Workspace.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

`WorkspaceConnectorCommandDbContext` contains only connector outbox commands and their audit history. The canonical profile-bound pooled factory reuses the existing mappings, idempotency and queue indexes, and command/audit cascade. Command processing retains the existing lease predicates and atomic finalization/audit transaction. The complete `AppDbContext` remains the migration authority and explicit profile-transfer maintenance model.

`WorkspaceSettingsDbContext` maps the existing workspace preferences table. Ordinary settings reads and saves use its canonical profile-bound factory; public settings fields, latest-record selection and currency normalization remain unchanged. Database-profile schema inspection retains the complete canonical maintenance model. The preference transfer handler receives a data-only operation and uses explicit source/target owner sessions; the opaque provider ID and latest-record selection are preserved, and the target preference save participates in the actual transfer transaction. Project lifetime admission for new connector commands and durable provider-effect reconciliation remain required dependent work; moving the outbox into its owner context does not establish those guarantees.

## Related Docs

Settings Core rendering and presentation now live in Workspace.UI and
Workspace.Presentation through Workspace.Contracts. This module keeps `/settings`, profile
and authority lifetime subscriptions and adapters to the existing owners. Data Sources,
Storage and API Access remain real active production hosts. Providers still redirects to
Agents. See the [partial completion and proof record](../../../docs/architecture/workspace-settings-core-ui-boundary.md).

API Access and Storage catalog administration now have their own UI boundaries.
The Storage selection field/dialog live in StorageSelection.UI; this module supplies
only their contextual display-safe read adapter over the existing catalog owner.
AgentDetailsDialog composes the renderer with its captured editor lifetime and retains
explicit Save and access normalization. Recovery, Data Sources and residual configuration
hosts remain here. See the [selection record](../../../docs/architecture/workspace-storage-selection-ui-boundary.md).

`SettingsPage` owns the `/settings` URL through `SettingsUrlCodec` (see
[navigation state](../../../docs/architecture/navigation-state.md)): `tab` (`data-sources`,
`storage`, `files`, `provider-history`, `secrets`, `api-access`; Workspace is the omitted
default and `providers` remains a Replace redirect to `/agents?tab=providers`), `secretId` on
Secrets, `profileId` on Data Sources, `storageId` on Storage and the committed account search
`q` and one-based `page` on API Access. Only identities and the committed search are written;
secret values, connection strings, passwords and tokens never are. Tab, record and explicit
search or paging intents push; defaults, owner-resolved identities and corrections replace.

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
