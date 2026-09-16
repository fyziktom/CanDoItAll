# CRM / HR Home UI sandbox

Backend-free scenario host for the real CRM / HR Home overview from
[CanDoItAll.CrmHr.UI](../../UI/CanDoItAll.CrmHr.UI/README.md). It references only that
rendering library (and, through it, BaseLib and Common). It has no database, no
`ICrmHrHomeQueryService`, no Web, module, AgentFramework or navigation dependency. Every
interaction updates deterministic local state and the intent line; the production secondary
tabs are host-owned chrome and are represented by a labelled slot, not duplicated.

Run from the repository root with the SDK selected by `global.json` and the live Components
checkout. Open the browser at 1600x1000 for the matched desktop frame.

## Parity: production assets

```sh
npm ci --prefix Tailwind
npm run tailwind:build
npm run crmhr:watch:parity
```

Open http://127.0.0.1:5397/crm-hr. Parity links the real production CSS with its physical
ContentRoot plus the BaseLib CSS, fonts, icons and scoped component CSS. It requires the
production theme to exist; a missing theme fails the build explicitly. Equivalent direct
command:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.CrmHr.UiSandbox --launch-profile "CrmHr sandbox" --property:CrmHrAssetMode=Parity
```

## Fast: local assets and bounded scanning

```sh
npm run crmhr:css:build
npm run crmhr:watch:fast
```

Open http://127.0.0.1:5398/crm-hr. Fast generates the ignored `wwwroot/css/crmhr-fast.css`
from `Tailwind/crmhr-fast.css`, which scans only the rendering library and this sandbox. It
never writes the production theme and can build when that asset is absent. Both modes use
separate bin/obj directories; a contradictory runtime profile fails explicitly.

## Scenarios

`/crm-hr?scenario=<token>&layout=matched|narrow|flexible`. Unknown tokens fall back to
`populated` and `matched`. Controls replace the current history entry so a reload restores
the context; the intent line stays transient.

| Token | Presentation |
|---|---|
| `populated` | Totals above the preview caps, five directory rows, one sensitive row, three opportunities with and without amounts |
| `loading` | Loading phase: placeholder stat values, loading state, route buttons still usable |
| `empty` | A successful read with zero totals: the section-specific empty copy |
| `failed` | Failed phase with safe copy and Retry; Retry resolves to the populated overview |
| `sensitive` | Several sensitive rows; the sensitive card stays narrower than the directory rows |
| `long-text` | Markup-looking and very long names, summaries and titles rendered as text |
| `null-optionals` | Missing summaries, no sensitive rows, an opportunity without an amount and with an unknown owner |
| `large-totals` | Totals in the hundreds and thousands while the previews stay capped at 5 / 3 / 6 |

All data is synthetic. No real person, organization, contact detail or confidential note is
present, and nothing here reaches an agent context.
