# Prompt Gallery UI sandbox

Backend-free scenario host for the real Prompt Gallery rendering surfaces from
[CanDoItAll.Prompts.UI](../../UI/CanDoItAll.Prompts.UI/README.md). It references only that
rendering library (and, through it, the Prompts contracts, SharedKernel and BaseLib). It has
no database, no `IPromptGalleryService`, no Web, module, AgentFramework or Curator dependency,
and it never invokes a provider. Every interaction updates deterministic local state and the
intent line.

Run from the repository root with the SDK selected by `global.json` and the live Components
and FileTools checkouts. Open the browser at 1600x1000 for the matched desktop frame.

## Parity: production assets

```sh
npm ci --prefix Tailwind
npm run tailwind:build
npm run prompts:watch:parity
```

Open http://127.0.0.1:5395/prompt-gallery. Parity links the real production CSS with its
physical ContentRoot plus the BaseLib CSS, fonts, icons and scoped component CSS. It
requires the production theme to exist. Equivalent direct command:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.Prompts.UiSandbox --launch-profile "Prompts sandbox" --property:PromptsAssetMode=Parity
```

## Fast: local assets and bounded scanning

```sh
npm run prompts:css:build
npm run prompts:watch:fast
```

Open http://127.0.0.1:5396/prompt-gallery. Fast generates the ignored
`wwwroot/css/prompts-fast.css` from `Tailwind/prompts-fast.css`, which scans only the
rendering library and this sandbox. It never writes the production theme and can build
when that asset is absent. Both modes use separate bin/obj directories; a contradictory
runtime profile fails explicitly.

## Scenarios

`/prompt-gallery?scenario=<token>&layout=matched|narrow|flexible`. Unknown tokens fall back
to `normal` and `matched`. Controls replace the current history entry so a reload restores
the context; filters, paging, favorites and the intent line stay transient.

| Token | Presentation |
|---|---|
| `normal` | 24 deterministic items, real filters, paging and favorites |
| `loading`, `empty`, `error` | List loading, no matches, search failure with Retry |
| `long-data` | 60 items with long titles, previews and full tag lists across three pages |
| `favorite-busy` | Favorite write in progress on the first item |
| `editor-new`, `editor-edit` | Editor dialog with an empty draft or a loaded item with versions and warning preferences |
| `editor-loading`, `editor-missing` | Editor loading and a missing target with Retry, never an empty draft |
| `editor-rejected`, `editor-busy`, `editor-unknown-result` | Backend rejection warning, disabled form during a command, unknown save outcome |
| `picker` | Embedded compact picker with the pinned chat provider/model filter |
| `compatibility`, `compatibility-blocked` | Warning dialog with suppressible warnings, and a blocking error that offers only Cancel |

Inline validation appears in `editor-new` when Save draft is clicked with an empty name or
content. Simulated saves in this sandbox do not replace the production effect-owning host
tests; see [Prompt Gallery UI boundary](../../../docs/architecture/prompt-gallery-ui-boundary.md).
