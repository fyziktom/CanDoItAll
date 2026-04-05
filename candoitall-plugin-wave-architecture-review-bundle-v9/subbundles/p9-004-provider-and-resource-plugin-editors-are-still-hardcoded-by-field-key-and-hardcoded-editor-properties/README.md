# P9-004 — Provider and resource plugin editors are still hardcoded by field key and hardcoded editor properties

Severity: **Critical**  
Gate: **HG-03**  
Module area: **Resources / Workspace**

## Problem
The manifests can list fields, but the editors cannot truly render or persist unknown plugin-defined fields. Every new email / LinkedIn / custom API plugin will still require core page/model edits, which means the platform is not plugin-first yet.

## Required architectural end-state
Introduce a generic connector configuration state bag and a generic renderer driven by ConnectorConfigFieldType. Known plugins may keep typed adapters, but the shared editor must round-trip unknown fields without page changes.

## Primary evidence
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor` lines 195-331: Resource editor renders fields via @switch(field.Key) across known keys only.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` lines 271-295: Provider editor renders fields via @switch(field.Key) across three known keys only.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 137-214: ResourceEditorModel is a hardcoded property bag for current plugins.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 113-143: ProviderProfileEditorModel is still a hardcoded current-plugin model.
