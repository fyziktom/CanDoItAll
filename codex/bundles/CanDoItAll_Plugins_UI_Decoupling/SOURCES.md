# Source register

Review: `fyziktom/CanDoItAll` / `components-decoupling` / `dd050d5a1489537207e073cac0838f40cde4340f` (2026-09-28).

File reads were pinned to the review SHA. The mutable branch/Actions URLs record observed metadata; do not treat later responses as the historical snapshot. Coverage below is explicit. No product build, test run, runtime benchmark or independent screenshot inspection is claimed.

The included shared foundation has a separate historical source register. It remains byte-identical; its reviewed commit/module status is not silently promoted to this audit.

## T01

[src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioWorkspace.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioWorkspace.cs)

Full current file: typed waits, independent reference generation, save settlement, fallback and disposal.

Observed Git blob: `b01162a3490076a58019a8ea5b53788fd75ac399`.

## T02

[tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabSandboxReviewTests.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabSandboxReviewTests.cs)

Full current 228-line file. Test source read; not compiled/discovered/executed by reviewer.

Observed Git blob: `a82b6fb914a53d67f73ede3e9d9b392f073407d6`.

## T03

[src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor)

Full current file: actual Unknown notification switch and existing route/session lifetime.

Observed Git blob: `9de273297e3938f9aba529303dca608bfd08b806`.

## T04

[docs/architecture/testlab-ui-boundary.md](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/docs/architecture/testlab-ui-boundary.md)

Lines 248 through the end: corrective R1/R2/C1 receipt. Claims of 77 passes, builds, watch/static gates are implementer-reported; ignored raw artifacts not inspected.

Observed Git blob: `442adf99d492ee15089383736680da84724d61c4`.

## P01

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor)

Lines 1-210: route, injections, catalog/rendered closure, real package dialog and InputFile.

Observed Git blob: `5a5f01f30cb44ba7f76d52725c8e8176454b84e6`.

## P02

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor)

Lines 211-470: initial/load/selection/log reads, mutations, upload, restart, grant keys, connection save and OAuth start.

Observed Git blob: `5a5f01f30cb44ba7f76d52725c8e8176454b84e6`.

## P03

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor)

Lines 471-end: disconnect, common busy/error/reload wrapper, editor reconstruction and callbacks. Together P01-P03 cover full page; pinned raw-file fetch corroborated it.

Observed Git blob: `5a5f01f30cb44ba7f76d52725c8e8176454b84e6`.

## P04

[src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj)

Full project source; literal edges are not an evaluated build graph.

Observed Git blob: `26e08c465896e3ec91e7a7c2c5bfea41a745fd22`.

## P05

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginDetails.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginDetails.razor)

Full file: six current sections, child contracts and index mapping.

Observed Git blob: `125df9199af0075a9780e072b9184856be474cfb`.

## P06

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginSettingsTab.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginSettingsTab.razor)

Full file: actual schema controls, onchange capture, validator rendering, context-dependent NavigationManager usage.

Observed Git blob: `4a18f5b50619c9f3f894682f1672f112ffadda2e`.

## P07

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginConnectionEditorState.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginConnectionEditorState.cs)

Full file: immutable-at-construction connection ID, mutable ConfigurationState/validation/dirty flag.

Observed Git blob: `9ca0df1d956209a8dd8b4478df3776a94d758f81`.

## P08

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs)

Lines 1-310 only: busy/editor keys, connection/OAuth policy helpers, icon and log rendering start. Tail beyond the returned range not audited.

Observed Git blob: `440af576939a4d45b277402e97fcf0a5bcc8e85b`.

## P09

[src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginGrantsTab.razor](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginGrantsTab.razor)

Full file: actual button action keys and callbacks.

Observed Git blob: `25de00b4c4fd26dff7bfa4d228e961e28d4a6911`.

## P10

[src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogModels.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogModels.cs)

Full file: catalog DTOs, documented enum/default/JSON semantics and install/update request shapes.

Observed Git blob: `909c23b8dbd42087e56acfae70c62aebf1f1cc63`.

## P11

[src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginRuntimeModels.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginRuntimeModels.cs)

Lines 1-270 only: grant/connection/settings contracts and log types/documented semantics through beginning of PluginLogItem. Rest not audited.

Observed Git blob: `63cc40e6147d850252e591d631fa34bee52ced23`.

## P12

[src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs)

Lines 1-290: grant/connection persistence and evaluator entry. Full evaluator middle not inspected in this range.

Observed Git blob: `81bf489433a6dee3b587158f02641bbe470a92df`.

## P13

[src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs)

Lines 360-end: cache tail, PluginSettingsService and recipe catalog. Lines 291-359 not included in this review coverage.

Observed Git blob: `81bf489433a6dee3b587158f02641bbe470a92df`.

## P14

[src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs)

Lines 1-240 only: catalog, bounded upload/staging, installation/extraction/persistence/restart/log sequence. Full archive validation/runtime-loader/restart implementation not audited.

Observed Git blob: `32cf270010b4f5011b39b0ec1f9792ea30b7d74a`.

## P15

[src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj)

Full project: references Models and SharedKernel; not a dependency-free assembly.

Observed Git blob: `55a3caab47cc892c57ed165ce91d33b5daaf3716`.

## P16

[src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj)

Full project: further abstraction references. No evaluated closure or runtime measurement performed.

Observed Git blob: `fa7a01bf6b4613df1f8fdd0a3d9255393d1b9d08`.

## P17

[tests/Components/CanDoItAll.Tests.Components/PluginsPageTests.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/tests/Components/CanDoItAll.Tests.Components/PluginsPageTests.cs)

Lines 1-270: seven Fact journeys and start of helper section. Remaining helper implementation not reviewed; test source not execution.

Observed Git blob: `501e86933be4a7d9b9afa7c5c2500641ec400346`.

## P18

[src/Modules/CanDoItAll.Modules.Plugins/OAuth/PluginOAuthService.cs](https://github.com/fyziktom/CanDoItAll/blob/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/OAuth/PluginOAuthService.cs)

Lines 1-210: full StartAsync and first callback checks. Full callback/token/disconnect implementation not audited.

Observed Git blob: `d11fb3074c056d90925c3ca49884bba3e40eacd1`.

## P19

[src/Modules/CanDoItAll.Modules.Plugins/Pages](https://github.com/fyziktom/CanDoItAll/tree/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins/Pages)

Full 13-entry Pages tree inventory. Child files without their own full-read source record were inventoried only, not audited in full.

Observed Git tree: `46005834747778d2808562996f6d5753e6569050`.

## P20

[src/Modules/CanDoItAll.Modules.Plugins](https://github.com/fyziktom/CanDoItAll/tree/dd050d5a1489537207e073cac0838f40cde4340f/src/Modules/CanDoItAll.Modules.Plugins)

Current module directory confirms Pages/Catalog/OAuth/Services subtree identities; not recursive code content.

## B01

[github-branch-metadata](https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling)

Branch rechecked after source inspection; current SHA matches audit, GitHub signature verification reports valid.

## B02

[github-commit-comparison](https://api.github.com/repos/fyziktom/CanDoItAll/compare/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b...dd050d5a1489537207e073cac0838f40cde4340f)

Comparison metadata and changed-file inventory: ahead 2, behind 0. No complete local checkout/diff execution.

## B03

[github-actions-query](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=dd050d5a1489537207e073cac0838f40cde4340f&per_page=10)

Current commit query reports zero workflow runs; not an assessment of local TRX.

## W01

[official-framework-documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0)

File selection replacement, explicit OpenReadStream size bound, avoid whole-file buffering and unsafe supplied filenames. Retrieved Microsoft page contains mixed version notices; only stated common InputFile/stream behavior is used, not lifecycle/version support claims.

## W02

[official-framework-documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch)

Project-reference scanning, watch inventory and hot reload documentation; actual local SDK behavior must be measured.

## Required current reads at execution

Current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md` and `.github/workflows/ci.yml` remain authoritative. Read current module registrations, API mappings, entire owners and unreviewed child/helper tails before edits. This list is an execution obligation, not a claim that every current line of those files was inspected by this source review.

The framework documentation supports narrowly stated browser-file/watch constraints. It does not prove the proposed implementation, an application vulnerability, a speedup or a green test gate.
