# Page Inputs: Prompts, Plugins, Settings, Resources

## PI-RESOURCES Resources `/resources`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`

Current display:
- `PageScaffold` resource management surface with summary tiles `Resources`, `Projects`, `Visible`, `Validated`.
- Form sections include `Identity and ownership`, `Location and connector details`, and `Metadata and capabilities`.
- Actions include `New resource`, `Save resource`, `Reset`, and `Delete`.

Current UX flows:
- User selects resource, edits ownership/location/connector/capabilities metadata, saves, resets, deletes, or creates a new resource.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panel 2.
- Full-width resource list/detail workspace with compact filters and detail form.

Function coverage confirmation:
- Covers CRUD and all current form sections.
- Reduces wasted width by using list/detail instead of stacked cards.

## PI-PLUGINS Plugins `/plugins`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`

Current display:
- Large page with tabs `Main info`, `Settings`, `Connections`, `Logs`, and `Grants`.
- Form sections include plugin identity/packages, connection descriptors, installation logs, runtime logs.
- Summary tiles include `Catalog`, `Installed`, `Enabled`, `Unavailable`, and `Packages`.
- Actions include install, install and enable, enable/disable, save settings, OAuth connect/disconnect, grant/revoke/deny, refresh, restart app, selected/all filters.

Current UX flows:
- User selects plugin, installs/enables/disables, configures settings and connections, reviews logs, grants/revokes permissions.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panels 3-4.
- Plugin TreeView/list grouped by catalog/enabled/installed/unavailable and tabbed detail pane.
- OAuth, connection, log, and grant surfaces use compact dialogs/inspectors.

Function coverage confirmation:
- Covers every current tab, summary state, and action family.
- Makes a complex admin page more professional without hiding permissions/logs.

## PI-PROMPT-GALLERY Prompt Gallery `/prompt-gallery`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`

Current display:
- Prompt library page with summary tiles `Prompts`, `Versions`, `Final`, and `Collections`.
- Detail tabs/forms include `Prompt identity`, `Draft content`, `Version history`, and `Usage history`.
- Actions include `New prompt`, `Add collection`, `Save draft`, `Create final version`, `Clone`, and `Reset`.

Current UX flows:
- User selects or creates prompt, edits identity/content, versions/finalizes, clones prompt, reviews usage history.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panel 5.
- Prompt TreeView/list grouped by collection/domain/version/tag with selected prompt detail tabs.

Function coverage confirmation:
- Covers prompt CRUD, collection, versioning, finalization, clone, and history.
- Adds clearer grouping for larger prompt libraries.

## PI-PROMPT-FACTORY Prompt Factory `/prompt-factory`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.Guidance.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactorySupportLaneTabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactoryDialogs.razor`

Current display:
- Canvas-first workbench without `PageScaffold`.
- Support lane tabs are `Canvas`, `Setup`, `Governance`, `Assembly`, and `Review`.
- Canvas uses `CanvasWorkbench`, component toolbox floating window, recommendation overlay, floating inspector, history toolbar, primitive previews.
- Setup edits prompt intent, language, app state, repositories, guidance, and project snapshot.
- Governance has internal views for selection, library, blocks, and templates.
- Assembly manages prompt inputs, files, storage/resource context, and selected resources.
- Review has internal views for readiness, preview, and delivery.
- Dialog component currently uses custom modal markup for prompt preview, component editor, and impact confirmation.

Current UX flows:
- User loads or creates session, configures setup, selects blueprint/template/components, builds flow, selects canvas nodes, previews prompt, edits component session text, saves session/draft/final, exports/sends, branches nodes, marks nodes used/validated/skipped, opens prompt artifact.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panels 6-7.
- Preserve canvas-first mode; use full desktop width, clear support lane tabs, consistent floating inspector, and shared dialog scaffolds for preview/editor/impact confirmation.

Function coverage confirmation:
- Covers all support tabs and custom dialogs.
- Explicitly keeps canvas workflow and prompt lifecycle actions intact.

## PI-SETTINGS Settings `/settings`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`

Current display:
- Settings page with `SecondaryTabs`.
- Summary tiles include `Providers`, `Enabled`, and `Secrets`.
- Form sections include provider API access, connector identity, auth/capabilities, secrets metadata/encrypted payload, workspace defaults, and notes.
- Database sources panel has transfer dialog.
- Actions include new/save/reset/delete provider/secret, create token, health, clear, save defaults.

Current UX flows:
- User manages provider profiles, secrets, workspace defaults, and database source transfer.
- Topbar DB switch currently points users away from the settings/DB deep-management destination.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panel 8 and `01-shell-baselib-corrected-proposal.png` panel 5.
- Settings remains detailed destination; shell bottom DB action opens flyout and DB dialog/settings entry.
- Database transfer remains a wide inspector dialog.

Function coverage confirmation:
- Covers settings tabs, providers, secrets, defaults, DB transfer, health, token, and CRUD flows.
- Moves always-available DB access into shell while preserving deep settings function.
