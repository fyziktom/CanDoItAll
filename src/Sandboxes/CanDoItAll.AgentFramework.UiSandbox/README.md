# Agent catalog sandbox

Run from the repository root with the SDK selected by `global.json` and the live Components and FileTools checkouts. Open the browser at 1600x1000 for the matched desktop fixture. The flexible-layout button removes the measurement frame.

Install the pinned Tailwind tooling once:

```sh
npm ci --prefix Tailwind
```

## Parity: production assets

Terminal one builds and watches the full application theme:

```sh
npm run tailwind:build
npm run tailwind:watch
```

Terminal two starts the real catalog with that theme:

```sh
npm run catalog:watch:parity
```

Open http://127.0.0.1:5391/agents. The compatible `Catalog sandbox` profile remains Parity. Its equivalent direct command is:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox --launch-profile "Catalog sandbox" --property:CatalogAssetMode=Parity
```

Parity links the actual production CSS with its physical ContentRoot, plus real BaseLib CSS, fonts, icons and component CSS isolation. It requires the production theme to exist. Use it for final visual regression checks.

## Fast: local assets and bounded scanning

Terminal one builds and watches only the catalog UI, sandbox and required Conversations UI source roots:

```sh
npm run catalog:css:build
npm run catalog:css:watch
```

Terminal two starts Fast:

```sh
npm run catalog:watch:fast
```

Open http://127.0.0.1:5392/agents. Equivalent direct command:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox --launch-profile "Catalog sandbox Fast" --property:CatalogAssetMode=Fast
```

Fast generates ignored `wwwroot/css/catalog-fast.css` inside this sandbox. It never writes or falls back to Web's `wwwroot/css/output.css`, and can build when that production asset is absent. A missing Fast output gives an explicit build error. Both modes use separate sandbox bin/obj configuration directories. Run one watch host at a time when editing their shared project dependencies.

Fast preserves the real BaseLib compiled theme, fonts, icons, tooltips and scoped component CSS. The live sibling already compiles its own utility sources, so Fast does not scan either sibling repository. No application shell, admin or reconnect CSS imports are needed by the specimen. Unrelated application styles are deliberately absent; Fast does not claim byte-for-byte Parity rendering.

The build chooses the mode. A contradictory runtime launch profile fails explicitly. In Development, `html[data-asset-mode]` and `/_dev/runtime` report the actual compiled mode; the probe also reports process/watch generation. These contain no application data. Browser refresh is enabled for normal direct CSS editing; launchBrowser remains false. [dotnet watch documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch) describes its hot reload and browser-refresh controls.

## Specimen and evidence

This host references only the lightweight UI project and renders the real AgentCatalogPanel, AgentSelectionCard, TreeView, Avatar and Tooltip. Its embedded snapshot has 40 agents, six teams, private-provider metadata, managed chat actions, long text and bundled avatars. Search `catalog-fixture` to isolate 12 custom cards. Selection is controlled local state; editor, membership, deletion and chat intents appear in the output line. There is no database, provider runtime or production effect graph.

Loading, empty and card states use the same components. Avatar fallback demonstrates the card's seeded image and BaseLib's explicit initials mode. A broken image URL is not the Avatar missing-value fallback contract.

The original extraction and managed observation results remain in `codex/bundles/UI_AgentCatalog_01_Extraction_Sandbox_Bundle`. Fresh direct local comparison belongs to `codex/bundles/UI_AgentCatalog_Harden_01_Development_Loop_Bundle`. It separates SDK update time from local edit-to-visible latency and retains failures. A small project graph or reduced CSS size alone is not a performance result.
