# Normalized Requirements

| ID | Requirement | Source | Owner | Proof |
| --- | --- | --- | --- | --- |
| `REQ001` | Active MCP server projects and MCP-only helper projects are migrated into `C:\repositories\CanDoItAll.Mcp`. | `N001` | `SB01` | File inventory, new solution build, main solution no longer referencing migrated projects. |
| `REQ002` | The MCP repository has its own solution containing only MCP source, MCP tests, and MCP helper projects. | `N001` | `SB01` | `dotnet build CanDoItAll.Mcp.slnx` and solution file review. |
| `REQ003` | MCP projects do not depend on main application projects; component dependencies use NuGet package references. | `N001` | `SB01` | Project reference audit and build proof. |
| `REQ004` | `tools/Reinstall-CanDoItAllMcps.ps1` builds MCP servers from the MCP repository and still syncs skills from the main repo. | `N001` | `SB02` | Resetup transcript, manifest review, config path review. |
| `REQ005` | Old MCP installs and historical MCP shadow build artifacts are removed from `repo://.artifacts`. | `N001` | `SB02` | Cleanup transcript and post-cleanup directory listing. |
| `REQ006` | The new MCP repository includes a useful root README and supporting docs for server inventory, build/test, resetup, settings, and artifact behavior. | `N001` | `SB03` | Docs file review. |
| `REQ007` | Final proof shows MCPs can build and reinstall from the new repository. | `N001` | `SB03` | Build/test transcript, resetup transcript, final bundle validation. |
