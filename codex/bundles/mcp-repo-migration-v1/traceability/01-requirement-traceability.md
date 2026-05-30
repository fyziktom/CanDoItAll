# Requirement Traceability

| Raw note | Exact wording | Normalized requirements | Owning subbundle | Bundle evidence | Planned proof |
| --- | --- | --- | --- | --- | --- |
| `N001` | `Move our MCPs servers to own repo.` | `REQ001`, `REQ002`, `REQ003` | `SB01` | `bundle://requirements/01-normalized-requirements.md`, `bundle://subbundles/01-01-mcp-solution-extraction/README.md` | New MCP solution build/test and main solution ref audit. |
| `N001` | `reinstall mcp script... updated path for the MCP repo to build mcp servers there, but skills it takes from this repo.` | `REQ004` | `SB02` | `bundle://subbundles/02-02-reinstall-tooling-and-artifact-cleanup/README.md` | Resetup transcript and source assertions for `$McpRepoRoot` and skill source path. |
| `N001` | `Assure then that all is possible to build and reinstall those mcps.` | `REQ007` | `SB03` | `bundle://subbundles/03-03-docs-and-final-validation/README.md` | Build/test/resetup transcripts and completed-stage validator. |
| `N001` | `remove from .artifacts all old installations of mcps and other things that are from some history builds` | `REQ005` | `SB02` | `bundle://subbundles/02-02-reinstall-tooling-and-artifact-cleanup/README.md` | Pre/post artifact listings and cleanup command transcript. |
| `N001` | `In new MCP repo you must add proper readme and docs about them.` | `REQ006` | `SB03` | `bundle://subbundles/03-03-docs-and-final-validation/README.md` | `C:\repositories\CanDoItAll.Mcp\README.md` and `C:\repositories\CanDoItAll.Mcp\docs\*.md` review. |
