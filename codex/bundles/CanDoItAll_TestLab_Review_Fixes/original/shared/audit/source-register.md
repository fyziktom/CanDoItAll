# Source register

Review: `fyziktom/CanDoItAll` / `development` / `7db3543ab437376baeca55089cb331fbe1b30483`. All S references below use this immutable commit. Returned blob identities are provenance from GitHub, not claims of local compilation or independently recomputed source hashes. A missing blob identity is explicit.

References such as [S12] in the guidance resolve here. Full means the file content was read; “full project metadata” is not a full module audit. A tree inventory proves placement only. Excerpts and repository-reported historical test results are not promoted to execution evidence.

## S01 · `docs/architecture/ui-component-seams.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/ui-component-seams.md)

Coverage: **full**. Canonical UI placement, two seam styles, state/effect ownership and proof rules.

Git object: `0deff237d31e2ce6b89baf28d207d391acd8931e`.

## S02 · `.github/copilot-instructions.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/.github/copilot-instructions.md)

Coverage: **full**. Engineering boundaries, Components workflow, HTTP control plane and gates.

Git object: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.

## S03 · `docs/testing.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/testing.md)

Coverage: **selected excerpts**. Opening/local/module sections (first response truncated); lines 340–625 covering CI/platform policy, documentation and portability.

Git object: `272f51153c28af81a28657cebf676a36a39b4a05`.

## S04 · `docs/architecture/prompt-gallery-ui-boundary.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/prompt-gallery-ui-boundary.md)

Coverage: **selected excerpts**. Ownership, contract decision, accepted baseline and behavior matrix; response truncated. Historical proof claims not rerun.

Git object: `not retained in the response`.

## S05 · `docs/architecture/crm-hr-ui-completion.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/crm-hr-ui-completion.md)

Coverage: **selected excerpts**. Dependency direction, seven-area completion map, retained roles and defect history; response truncated. Historical proof claims not rerun.

Git object: `not retained in the response`.

## S06 · `CanDoItAll.slnx`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/CanDoItAll.slnx)

Coverage: **selected excerpts**. Product graph and UI/sandbox placements; large response truncated. Product-only test separation corroborated by S03.

Git object: `not retained in the response`.

## S07 · `Directory.Build.targets`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/Directory.Build.targets)

Coverage: **full**. Source sibling rewrite, preflight validation and template-copy policy.

Git object: `72f2ab40cbb5c8a873ac9210c6eb5a5ad4a2325b`.

## S08 · `package.json`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/package.json)

Coverage: **full**. Catalog/Prompts/CRMHR sandbox and Tailwind scripts.

Git object: `7b3d6bae22aa329020ec727a3b291b6778612608`.

## S09 · `src/UI/CanDoItAll.Prompts.UI/CanDoItAll.Prompts.UI.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/UI/CanDoItAll.Prompts.UI/CanDoItAll.Prompts.UI.csproj)

Coverage: **full**. Direct renderer references and template-copy setting.

Git object: `c0a5d9f331650990d84334d66c573cae12b8d8d1`.

## S10 · `src/UI/CanDoItAll.CrmHr.UI/CanDoItAll.CrmHr.UI.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/UI/CanDoItAll.CrmHr.UI/CanDoItAll.CrmHr.UI.csproj)

Coverage: **full**. Direct renderer references, shared families and template-copy setting.

Git object: `b458ff166c2e0f333f318ea6a495d2a3fc32d038`.

## S11 · `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.View.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.View.cs)

Coverage: **full**. Actual host-implemented workspace view and forwarding/event ownership.

Git object: `da82d7ac8cbcce83226a1fe33e092bb5c811564e`.

## S12 · `src/UI/CanDoItAll.CrmHr.UI/CrmHrWorkspaceSurface.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/UI/CanDoItAll.CrmHr.UI/CrmHrWorkspaceSurface.cs)

Coverage: **full**. Draft-bound EditContext and host render notifications.

Git object: `1b302b4af7580b2a693f9658a57582bff21e9320`.

## S13 · `src/UI/CanDoItAll.CrmHr.UI/Parties/CrmHrDirectoryWorkspaceSurface.razor`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/UI/CanDoItAll.CrmHr.UI/Parties/CrmHrDirectoryWorkspaceSurface.razor)

Coverage: **lines 1–160**. Real renderer, children, dialog, form/context and draft bindings.

Git object: `f8ce6a1270fe4da9bbb1bb52a0a77aa084ae285a`.

## S14 · `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrHomePage.razor`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrHomePage.razor)

Coverage: **full**. Route, instance read session, typed intent mapping and disposal.

Git object: `687a599cf3f44d231a80d65206150c87795ee5e9`.

## S15 · `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj)

Coverage: **full**. UI-only project reference; Parity/Fast content and output checks.

Git object: `292fda4a26f45b63d75dbdb750f2c6209d64cb23`.

## S16 · `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/Program.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/Program.cs)

Coverage: **full**. Real Razor/BaseLib/Charts with sandbox read registrations and mode validation.

Git object: `554cafe9a2f26871e6d0d31d24abbe3f09db0ded`.

## S17 · `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/CanDoItAll.AgentFramework.Workflows.UI.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI/CanDoItAll.AgentFramework.Workflows.UI.csproj)

Coverage: **full**. Existing workflow rendering project outside src/UI.

Git object: `b277df99ad788938864666859adde3575f530509`.

## S18 · `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.UI/CanDoItAll.AgentFramework.Llm.SimpleChats.UI.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.UI/CanDoItAll.AgentFramework.Llm.SimpleChats.UI.csproj)

Coverage: **full**. Existing Simple Chats rendering project outside src/UI.

Git object: `a719f3875126acf4475f4c7f5102828be91f98e5`.

## S19 · `.github/workflows/ci.yml`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/.github/workflows/ci.yml)

Coverage: **lines 1–210**. Source dependency resolution, FileTools pin, stable setup and PostgreSQL 18 verification. Rest of workflow not fully read.

Git object: `9b99731fdb9d3c360871fc66a50daaab7fcdcf85`.

## S20 · `global.json`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/global.json)

Coverage: **full**. SDK 10.0.302 and roll-forward setting.

Git object: `6d5813f02d3a0627270a72166b8b84c45bc97a35`.

## S21 · `Directory.Build.props`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/Directory.Build.props)

Coverage: **full**. Central fallback versions and default item exclusions.

Git object: `41aa7879429138490f6a9abf5d63eff40c994c2a`.

## S22 · `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGallerySearchSession.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGallerySearchSession.cs)

Coverage: **full**. Generation fencing, favorite/refresh classification and observed operation-CTS disposal gap.

Git object: `ec3c26be2291573d214771ef5979bfaecc805231`.

## S23 · `tests/Components/CanDoItAll.Tests.Components/CrmHrUiModuleBoundaryTests.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/tests/Components/CanDoItAll.Tests.Components/CrmHrUiModuleBoundaryTests.cs)

Coverage: **full**. Actual boundary tests, permitted ports, view signatures and module host roles.

Git object: `81659b25375b07634c443c4b0836c431053bb3de`.

## S24 · `tests/Components/CanDoItAll.Tests.Components/CrmHrUiBoundary.cs`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/tests/Components/CanDoItAll.Tests.Components/CrmHrUiBoundary.cs)

Coverage: **full**. Actual reflection traversal and unresolved-assembly/suffix limitations.

Git object: `2ebe789768cecbe133058e700f7303e0328c6b3e`.

## S25 · `AGENTS.md`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/AGENTS.md)

Coverage: **full**. Canonical family/product instructions and mandatory portability closure.

Git object: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.

## S26 · `src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `9ae399163bb3a6ad2d8523e46b5c550a3a308302`.

## S27 · `src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `0e7b6d2b68c6ded5c5c3220fab4a5e6ee0750063`.

## S28 · `src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `1b4d0348eb301e51e210ae2f9c371f240cbda2e3`.

## S29 · `src/Modules/CanDoItAll.Modules.Memory/CanDoItAll.Modules.Memory.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Memory/CanDoItAll.Modules.Memory.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `4643b4aea6b3d6f70b36f1ce61ee3c0bc99c2cc1`.

## S30 · `src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `26e08c465896e3ec91e7a7c2c5bfea41a745fd22`.

## S31 · `src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `53a9755764649a74e16e401bf63beba72ebfc0bf`.

## S32 · `src/Modules/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `f7d404910217b1bec6605fb5b47ff65c72f72016`.

## S33 · `src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `fe1da7d4fd429cbdac77e54e2442f702d07c315d`.

## S34 · `src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `291195b373393824292b24c60b90a519449abd24`.

## S35 · `src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `cf59aea48c3e8ba4f16a6c3a301bc6f8a4552894`.

## S36 · `src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `1544367e648d7ed8fae3b3efd8172a8f15e80938`.

## S37 · `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj)

Coverage: **full project metadata**. SDK, packages and direct project references; not a component/method audit of this entire module.

Git object: `42159259da6549c98989871a7dc6961eec933b80`.

## S38 · `src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj)

Coverage: **full**. Current Agents renderer direct references.

Git object: `3922611e7b630fb741ed151925a3f7d6b06a27c4`.

## S39 · `src/UI`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/tree/7db3543ab437376baeca55089cb331fbe1b30483/src/UI)

Coverage: **nonrecursive directory inventory**. Names/locations only; no claim that all descendant code was read.

Git object: `cb619a30ad11cee3234abeb428f12551a1563674`.

## S40 · `src/Modules`

[Open reviewed source](https://github.com/fyziktom/CanDoItAll/tree/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules)

Coverage: **nonrecursive directory inventory**. Names/locations only; no claim that all descendant code was read.

Git object: `a8c85bc177690f00e93153bf95842e27a617a778`.

## F01 · dotnet watch

[Official documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch) · accessed 2026-09-28.
Reference-graph watching, --list, hot reload/restart and exclusion semantics.

## F02 · Evaluate MSBuild items and properties

[Official documentation](https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties?view=vs-2022) · accessed 2026-09-28.
Evaluated item/property queries; not an executed project evaluation.

## F03 · Blazor synchronization context (.NET 10)

[Official documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0) · accessed 2026-09-28.
Async reentrancy, disposal and renderer-dispatch considerations.
