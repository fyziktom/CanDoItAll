# Source Artifacts

- User-provided screenshot of `/processes` shows the target header style: one compact command strip with page eyebrow, status badges, and icon-only actions.
- Current implementation reference: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`.
- Component catalog evidence gathered through CanDoItAll Components MCP:
  - `PageScaffold` is the shared dense page shell.
  - `PageHeader` is the existing shared page-header primitive.
  - `StatusBadge` is the existing compact count/status chip.
  - `TooltipTarget` already supports `Delay`, `Position`, `Text`, and rich tooltip content.
