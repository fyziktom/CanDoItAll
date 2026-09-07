# Agent Framework UI sandbox

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

Terminal one builds and watches only the rendering UI, sandbox and required Conversations UI source roots:

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

## Reload context

The development specimen accepts `scenario=normal|loading|empty|card-states|avatar-fallback`, `layout=matched|flexible`, and optional `agentId`/`teamId` query values on `/agents`. IDs must exist in the embedded fixture. Invalid values normalize to Normal/Matched or no selection. Controls replace the current history entry; a browser reload restores that context. Search text, favorite toggles and recorded intent text remain transient.

For a reproducible normal specimen, start at `/agents?scenario=normal&layout=matched`. This query belongs only to the sandbox and does not change production routing.

## Capabilities specimen

Use `/agents?specimen=capabilities&scenario=baseline&layout=matched`. Missing or unknown specimen tokens retain the catalog default and its existing scenario/layout/agentId/teamId behavior. Both specimen owners normalize with replace-history. A capabilities target that is malformed or absent stays an explicit failed target; retry does not select another agent. Raw filters, access-rule text and intent output are transient.

The capability scenario selector has explicit named tokens in CapabilitiesSandboxContext. It covers loading, failed and missing targets, empty states, every supported kind/proof state, long content, assignment/recovery eligibility, preview and Curator/acknowledgement presentations. All IDs are deterministic. AgentCapabilitiesSurface, AgentCapabilityList and the shared avatar action are the real production rendering components, including BaseLib tree, tags, cards, tooltips and isolated CSS. The matched frame preserves the full-app left/top coordinates at 1600 x 1000.

The embedded baseline is the public rendering snapshot captured before Capabilities-03 extraction. Search `capa03-benchmark` for the matched three-card timing fixture. Other scenarios use small immutable samples. Clicks update only controlled sample state or the output line; they never register or invoke workspace, persistence, provider, diagnostic, chat or external endpoint services. Recovery eligibility is supplied as presentation flags, not calculated from application outcome types.

Both modes use the same specimens. Fast scans the existing UI/UiSandbox/Conversations roots, with live BaseLib compiled CSS; no Module or broad Components scan root is added. Capabilities correctness, source movement, direct-watch baseline and comparison evidence belong to `codex/bundles/UI_AgentCapabilities_03_Extraction_Sandbox_Bundle`. Small graph size alone is not performance proof.

The themes also import `Tailwind/main/component-layout-utilities.css`, the shared production compatibility rules for BaseLib Split's runtime-composed responsive classes. These rules intentionally remain unlayered to match the live BaseLib cascade. Class generation alone is insufficient here; the browser acceptance verifies actual computed FilterBar layout in both modes.
