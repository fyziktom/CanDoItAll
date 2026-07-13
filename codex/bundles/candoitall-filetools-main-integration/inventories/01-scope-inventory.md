# Scope Inventory

## Product Areas

| Area | Current owner | Planned owner/boundary | Owning subbundles |
| --- | --- | --- | --- |
| Storage read/write/delete | Infrastructure `IStorageDriver` family | Preserve; share provider-specific path/transport collaborators only where justified | SB02-SB04 |
| Storage browse/list/stat | Missing | Infrastructure-native `IStorageBrowseDriver` family and registry | SB02-SB04 |
| Large-source paging/search | FileTools filesystem currently fully enumerates/sorts/hashes before page one | truthful provider order/cursor or bounded index, typed work/state budgets, observable partial outcomes | SB02-SB05, SB08, SB10, SB18 |
| FileTools mapping | Missing | new outer `CanDoItAll.FileTools.Integration` adapter project | SB06-SB08 |
| Stable scope contracts | Missing | small `CanDoItAll.FileTools.Integration.Abstractions` project | SB06 |
| Authorization/handles | unsigned reference/path checks | outer integration + Web endpoint boundary | SB07 |
| Cache/revision | missing | outer integration; HybridCache memory primary and in-memory revision first | SB08 |
| Project scope | page-local project filters/hierarchy | directly tested Projects-owned projection and scope provider | SB10, SB12 |
| Workbench project/node scope | local opener, existing asset double-click/dialog, and page partials | direct known-asset FileInteraction dialog plus separate focused collection scope resolver/coordinator/browser window | SB13, SB16 |
| Process-run roots | launch variables/policies spread across Processes/Workbench | Processes-owned root policy and scope provider | SB14 |
| Resource sources/promotion | connector registry without generic storage object | Resources-owned source catalog and promotion command | SB15 |
| Known-file view/edit | several Workbench dialogs/branches | FileInteraction host coordinator/adapters and selected renderer packages | SB10, SB16 |

## Test Homes

| Proof | Preferred project |
| --- | --- |
| Storage contracts/drivers/cache-independent behavior | `repo://tests/Unit/CanDoItAll.Tests.Unit` |
| Persistence/config/endpoint/composition | `repo://tests/Integration/CanDoItAll.Tests.Integration` |
| Focused Razor state and callbacks | `repo://tests/Components/CanDoItAll.Tests.Components` |
| User flows/layout/overlays/console/network | `repo://tests/Playwright/CanDoItAll.Tests.Playwright` |
| Package product behavior | FileTools repo test projects and package validation scripts at pinned commit |
| Performance/scale | unit/integration harnesses with instrumented providers/transports, generated large-directory fixtures, repeated runtime/allocation counters, and direct zero-browser-call spies |

## Explicitly Deferred

- Mobile/small/medium/tablet behavior.
- Durable distributed revision/backplane and distributed HybridCache secondary.
- Hostile-root filesystem guarantees beyond current root confinement/reparse policy.
- A full diff engine, Office editors, or loading every FileInteraction renderer package.
- General FTP transport modernization beyond the minimum adapter/test seam required for truthful browsing.
