# Target Solution

## Architecture

- Add durable `Tags` metadata to `ProviderProfile`, `ProviderProfileEditorModel`, `CapabilityCatalogItem`, and `CapabilityEditorModel`.
- Normalize seeded/default provider and capability tags through the existing seed normalization path so existing workspaces receive useful tags on read.
- Replace the Agents shell provider tab with a new AgentFramework-backed provider panel. Keep the Workspace provider management panel available in Workspace settings.
- Add provider/capability tree builders following the existing plugin tag tree pattern.
- Rework `AgentCapabilitiesPanel` into a two-pane tree/detail surface with desktop filter toolbar and compact card grid.
- Add a capability details dialog that edits shared metadata for all capabilities, typed MCP parameters for MCP servers, and tag-only guarded editing for built-in tools.
- Add a capability setup wizard dialog using `Steps` and an `InputFile` upload/drop-zone pattern for `SKILL.md`.

## Wizard Visual Thesis

Operational admin dialog: dense, quiet, and explicit. The left step rail gives orientation, the center owns editing, and the right rail provides live review/validation without adding explanatory marketing copy.

## Imagegen Planning Artifacts

- Step 1 Type/Identity proposal generated with `imagegen`.
- Step 2 MCP Configuration proposal generated with `imagegen`.
- Step 2 Skill Configuration proposal generated with `imagegen`.
- Step 3 Review proposal generated with `imagegen`.

Generated images are planning artifacts only and are not acceptance proof.

## Wizard ASCII Layouts

### Step 1 Type And Identity

```text
+ Capability setup ----------------------------------------------------------+
| [Steps]                  | Type                                           | Review        |
|  1 Type                  | [ MCP server ] [ Skill ]                      | type          |
|  2 Configuration         | Name:        [________________________]       | name          |
|  3 Review                | Key:         [________________________]       | key           |
|                          | Description: [________________________]       | description   |
|                          |                                                |               |
|                                                                       Next |
+----------------------------------------------------------------------------+
```

### Step 2 MCP Configuration

```text
+ Capability setup ----------------------------------------------------------+
| [Steps]                  | MCP connection                                 | Live review   |
|  1 Type                  | Transport [stdio v]   Command [npx]           | transport     |
|  2 Configuration         | Arguments [textarea: one arg per line]         | command       |
|  3 Review                | Working directory [______________]             | arguments     |
|                          | Allowed tools [textarea: one tool per line]    | allowed tools |
|                                                                  Back Next |
+----------------------------------------------------------------------------+
```

### Step 2 Skill Configuration

```text
+ Capability setup ----------------------------------------------------------+
| [Steps]                  | Skill source                                   | Live review   |
|  1 Type                  | Source mode [Path] [Upload SKILL.md]           | source mode   |
|  2 Configuration         | Skill root path [____________________]         | path/upload   |
|  3 Review                | Upload SKILL.md [drop zone + InputFile]        | preview       |
|                          | Inline instructions [textarea]                 | approval      |
|                          | [ ] Script approval required                   |               |
|                                                                  Back Next |
+----------------------------------------------------------------------------+
```

### Step 3 Review

```text
+ Capability setup ----------------------------------------------------------+
| [Steps]                  | Review                                         | Validation    |
|  OK Type                 | Metadata table                                 | OK metadata   |
|  OK Configuration        | Configuration table                            | OK identifier |
|  3 Review                | Tags [TagEditor]                               | ! auth/tools  |
|                          |                                                | Ready to save |
|                                                          Back Save capability |
+----------------------------------------------------------------------------+
```

## Production Behavior Artifact Matrix

| Signal/state | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Provider tags | Provider editor and seed normalizer | Provider tree, search/filter, future prompt tag workflows | Saved in AgentFramework provider catalog / Workspace extra settings. |
| Capability tags | Capability dialog/wizard and seed normalizer | Capability filters, tree/card metadata, future prompt tag workflows | Saved in AgentFramework capability catalog. |
| MCP typed configuration | MCP detail dialog and wizard | MAF MCP runtime builder | Saved as existing JSON schema fields (`transport`, `command`, `arguments`, `workingDirectory`, `allowedTools`, `approvalMode`). |
| Uploaded skill instructions | Skill wizard `InputFile` reader | MAF inline skill builder | Saved in `inlineSkill.instructions` configuration. |
