# P9-005 — Custom plugins still persist bogus legacy enum identity

Severity: **Critical**  
Gate: **HG-04**  
Module area: **Resources / Workspace**

## Problem
Plugin key is not yet the single source of truth. Custom plugins can end up carrying fake legacy enum values, which will leak into summaries, reports, filters, or future behavior. That directly undermines the plugin platform.

## Required architectural end-state
Demote ProviderKind / ResourceKind to compatibility-only optional fields or retire them. New/custom plugin flows must persist plugin key as the authoritative identity and must never synthesize a legacy enum just to satisfy old code.

## Primary evidence
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs` lines 189-230: EnsureLegacyResourceKind / ResolveLegacyResourceKind still synthesize enum identity from plugin key.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 363-370: SaveAsync persists entity.ResourceKind = connectorPlugin.LegacyResourceKind ?? model.ResourceKind.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 25-33: ProviderProfile still has active ProviderKind property.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 101-119: Provider summaries and editor model still expose ProviderKind as an active surface.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 313-316: SaveProviderAsync persists entity.ProviderKind = providerPlugin.LegacyProviderKind ?? model.ProviderKind.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs` lines 230-281: NewProvider(...) still defaults plugin identities through legacy ProviderKind presets.
