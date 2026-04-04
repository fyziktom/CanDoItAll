# Current State

## Zyphonote

- `C:\repositories\zyphonote\src\App.Blazor\Pages\Progress.razor` currently contains four top-of-page comparison sections:
  - original Stack version
  - Grid version
  - Grid + Row + Column version
  - responsive Row/Column version
- The user only wants the responsive Row/Column version to remain in Zyphonote.

## BaseLib Layout Primitives

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\Grid.razor` now carries inline `display:grid`, alignment support, and a default inherited column span token.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\Row.razor` can inherit or override parent grid tracks and responsive column templates.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\Column.razor` now supports responsive spans plus content alignment and orientation.
- `C:\repositories\CanDoItAll\Tailwind\foundation\radzen-layout.css` now includes `sm`, `lg`, and `2xl` responsive span rules for `.rz-col`.

## Components Sandbox

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Layout.razor` already demonstrates higher-level layout primitives such as `PageScaffold`, `ListDetailShell`, and `StickyActionFooter`.
- The current sandbox catalog uses `SandboxCatalogRegistry` groups and examples, but the Layout group does not yet contain a dedicated “how to compose Grid/Row/Column/Stack” reference page.

## Components MCP

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogService.cs` currently indexes BaseLib and CanvasLib components, sandbox groups, sandbox examples, and typed canvas contracts.
- The component MCP already has real component metadata, but it does not yet encode the newly learned layout composition rules, nor does it expose concrete consumer examples from `CanDoItAll.Web` and related modules.

## Install And Guidance

- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1` currently publishes and wires `candoitall_dotnetwatch`, `candoitall_sshops`, and `candoitall_projectstructure`, but not `candoitall_components`.
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1` syncs repo-managed skills, so a new skill in `codex\skills` will install automatically.
- `C:\repositories\CanDoItAll\codex\README.md` currently documents the existing skill pack, but not component MCP usage guidance.
