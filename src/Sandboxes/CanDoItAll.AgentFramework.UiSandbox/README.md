# Agent catalog sandbox

Run from the repository root with the SDK selected by global.json and the live sibling Components and FileTools checkouts:

```powershell
npm --prefix Tailwind ci
npm --prefix Tailwind run build
dotnet watch --project src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox --launch-profile "Catalog sandbox"
```

Keep `npm --prefix Tailwind run watch` running in another terminal for theme edits. The repository dotnet-watch MCP SourceWatch lane starts that companion itself; do not start a second copy in that lane. Open http://127.0.0.1:5391/agents at 1600Ă—1000 for the matched desktop fixture. The flexible-layout button removes the measurement frame.

This host references only the lightweight UI project. It renders the real AgentCatalogPanel, AgentSelectionCard, TreeView, Avatar and Tooltip with an embedded immutable snapshot. Selection is local controlled state. Editor, membership, deletion and chat intents appear in the output line; the sandbox has no database, provider runtime, or production effects.

Normal includes 40 agents, six teams, private-provider metadata, managed chat actions, long text and bundled avatars. Search for `catalog-fixture` to isolate the 12 custom cards. Loading, empty and card states use the same components. Avatar fallback demonstrates the card's seeded image and BaseLib's explicit initials mode. A broken image URL is not the existing Avatar component's missing-value fallback contract.

The shared Tailwind output is linked as a static web asset with its actual physical ContentRoot. It is not copied into source or obtained through a Web project reference. Both its bytes and live updates must be checked; an HTTP 200 alone is insufficient. See [Microsoft's static asset documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0).

The development-only /_dev/runtime probe exposes readiness, process/watch generation and managed-launch ownership. It carries no application data. MetadataUpdateHandler reports applied managed updates; the SDK supports a handler containing only UpdateApplication.

Reproducible full-app/sandbox comparison and preserved pre-extraction evidence are in codex/bundles/UI_AgentCatalog_01_Extraction_Sandbox_Bundle. Cold startup is measured separately from warm Razor, C# and CSS edits. A small dependency graph by itself is not performance proof.
