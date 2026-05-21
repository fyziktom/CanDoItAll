# Original Request

Source: user request and console output from 2026-05-21.

The user reported that after moving agent templates under the repository `Templates` folder, MCP reinstallation fails because the MCP build copies that folder into a long shadow artifact path. The failure happens while running:

```powershell
PS C:\repositories\CanDoItAll\tools> .\Reinstall-CanDoItAllMcps.ps1
```

Representative failure lines:

```text
C:\repositories\CanDoItAll\Directory.Build.targets(8,5): error MSB3021: Unable to copy file "C:\repositories\CanDoItAll\Templates\Agents\teams\visual-automation-templates\members\screenshot-review-storage-agent\settings.json" to "C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\builds\4b2d2de61f70c11c22fe692d08940a823b118a599936e8537daa87743a8f48b9-1779366809\bin\CanDoItAll.Mcp.Core\release\Templates\Agents\teams\visual-automation-templates\members\screenshot-review-storage-agent\settings.json". Path: C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\builds\4b2d2de61f70c11c22fe692d08940a823b118a599936e8537daa87743a8f48b9-1779366809\bin\CanDoItAll.Mcp.Core\release\Templates\Agents\teams\visual-automation-templates\members\screenshot-review-storage-agent exceeds the OS max path limit.
Shadow build failed with exit code 1.
```

Actionable user notes:

1. Moving agent templates into `Templates` was correct and should not be reverted.
2. MCP server installation should not need those templates.
3. MCP reinstall must build MCPs, set them up, and still set up skills as it already does.
4. Shortening the shadow build hash may help but is not a full solution.
5. The MCP-related projects do not appear to have a strong dependency on a project that should load `Templates`.
6. Improve the MCP reinstallation build approach so MCP projects are built with a standard repo `bin\Release` build and final build outputs are copied into artifacts instead of building the whole output directly under the shadow artifact path.
7. Validate that the improved reinstall process works.
