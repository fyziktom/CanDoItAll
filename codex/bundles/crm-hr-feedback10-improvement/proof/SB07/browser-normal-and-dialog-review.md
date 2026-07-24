# SB07 Browser Normal And Dialog Review

Review date: `2026-07-24`

## Environment And Interactions

- Release application listener: `http://127.0.0.1:5032`
- Viewport: `1800x1100`
- Directory interaction: opened `CRM Directory`, verified the catalogue occupied `1585/1585` available pixels, inspected `18` cards from `78` matching records, confirmed the bounded results region had `overflow: auto` and real overflow, used `Next` to reach `Page 2 / 5`, returned to the first page, and opened Amina Hassan.
- Workforce interaction: opened `CRM Workforce`, verified the catalogue occupied `1585/1585` available pixels, inspected `12` cards from `32` matching records, confirmed the results region was independently scrollable, used `Next` to reach `Page 2 / 3`, returned to the first page, and opened Lucas Ferreira.
- Dialog interaction: inspected the complete tab rows and content regions. Amina exposed `Profile`, `Contacts`, `Privacy`, `Activity`, and `Relations`. Lucas exposed `Overview`, `Profile`, `Skills 3`, `Allocations`, and `History`; the manager was Grace Kim and the allocation view contained `Tentative 2026-09-01 -> 2026-09-30 / 30%`.

## Inspected Screenshots

| State | Artifact | Bytes | SHA-256 | Visual finding |
| --- | --- | ---: | --- | --- |
| Directory catalogue | `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/page-2026-07-24T17-31-37-844Z.png` | `325114` | `4BA6BD4749408040358AD11F653642DD6F229F3ECA3F5A4263615728289C3F10` | `CRM Directory`, `78` matching records, multiple card rows, visible `Page 1 / 5` pager and `Next`, full-width primary surface. |
| Amina directory dialog | `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/page-2026-07-24T17-24-18-204Z.png` | `211674` | `D848E6D1E826C8FB55B45A084077FF85920E6C538A983731497D2F16EF8629A5` | All five tabs fit; the body owns content scrolling; the header and action footer remain visible; no lateral clipping. |
| Workforce catalogue | `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/page-2026-07-24T17-24-40-575Z.png` | `294636` | `A287646F9AC440711D64FEB2C22EC58D07A2BA412CC4074A0267B23CD4674B01` | `CRM Workforce`, `32` matching records, twelve cards, visible `Page 1 / 3` pager and internal scrollbar, full-width primary surface. |
| Lucas workforce dialog | `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/page-2026-07-24T17-25-19-286Z.png` | `210403` | `8FEEBC96080D5DE6C0259784443B37173CF0EE53F8A5B2E5208116860D72703D` | All five tabs fit, the selected profile identifies Grace Kim as manager, dialog content remains within its scroll owner, and the footer stays usable. |

Each image was opened and visually inspected at closure. File existence, byte length, and digest were checked independently.

## Semantic And Adversarial Proof

- Semantic positive: both populated catalogues exposed more than one source page, the pager changed the visible page, record selection opened the controlled details dialog, and closing the overlay returned to a usable catalogue.
- Shallow-pass trap rejected: the measured catalogue width equals the available container width; both result regions had actual overflow and were independently scrollable; page changes reached the second server page. This is not a permanent detail pane with cosmetic modal chrome or a pager label over one in-memory list.
- Adversarial negative: the bounded scroll mode remains typed and opt-in. The default picker-dialog path is covered by `PagedRecordBrowserTests`, and close-during-load generation invalidation is covered by `CrmHrDirectoryPageFreshnessTests`.
- Anti-stub audit: the inspected application used persisted API-created records. No fixture-only branch, fallback full-list control, hidden permanent editor, `TODO`, or `NotImplemented` path supplied the rendered states.
- Overlay conclusion: the Directory and Workforce record dialogs, tab content, inner scrolling, and fixed action regions were usable without clipping. The existing focused dialog tests cover deep-link selection, close/reopen, and stale-load behavior that a still image cannot prove.

## Focused Regression Command

```powershell
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build -nologo --filter "(FullyQualifiedName~PagedRecordBrowserTests|FullyQualifiedName~CrmHrCatalogDialogTests|FullyQualifiedName~CrmHrDirectoryPageFreshnessTests|FullyQualifiedName~CrmHrNavigationTests|FullyQualifiedName~CrmHrWorkspaceFreshnessTests)"
```

- Exit code: `0`
- Result: `37 passed`, `0 failed`, `0 skipped`
- Elapsed: `1m50s`

## Decision

`Pass`. The rendered normal and open-dialog states agree with the source paging, typed-scroll, route-title, and freshness tests. `CP-07` may close.
