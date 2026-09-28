# Preparation source register

Review date: **2026-09-28**. Repository: `fyziktom/CanDoItAll`, branch `development`, observed commit `7db3543ab437376baeca55089cb331fbe1b30483` (commit timestamp `2026-09-27T20:36:18Z`). The review revision is provenance, **not an execution pin**. A final connected branch read returned the same HEAD.

The selected module was inspected through actual connected GitHub reads. Search results from the default branch were used only as file locators. The remaining candidate map also uses the unchanged shared v3 audit at the same commit; that inherited evidence is not presented as a fresh full source audit. No application build, test discovery/run, database/browser run or watch measurement was performed. See [review limitations](REVIEW_NOTES.md#what-preparation-did-not-verify).

Machine-readable details and observed blob/tree identities are in [sources.json](sources.json). Coverage below is explicit: a directory or a partial read cannot certify every behavior in that module.

## C01 — `development`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/branches/development) · **Coverage:** fresh branch metadata at start and at review end; unchanged HEAD; not a branch checkout.

Observed review HEAD and signed commit metadata.

## C02 — `docs/architecture/ui-component-seams.md`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/architecture/ui-component-seams.md) · **Coverage:** fresh full text; blob identity retained from identical-revision shared S01.

Canonical placement, two valid seam shapes, state/effect and proof requirements.

## C03 — `src/Modules/CanDoItAll.Modules.Collaboration`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/git/trees/fce53666521f155b2ab07cdaf589604f3336dc15?recursive=1) · **Coverage:** fresh complete recursive tree, not all file bodies.

One feature page plus code-behind; actual module file inventory.

## C04 — `src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor) · **Coverage:** fresh full text in requested ranges 1-220 and 221-460 through EOF.

Actual full workspace renderer closure, forms, component use, links, test IDs.

## C05 — `src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor.cs) · **Coverage:** fresh full text.

Route/selection, read lifecycle, drafts, command admission and post-await completion risks.

## C06 — `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationContracts.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationContracts.cs) · **Coverage:** fresh full text.

Plain request/read models and annotated editor models.

## C07 — `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationModels.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationModels.cs) · **Coverage:** fresh full text.

Six enums, four entity records, EF configurations, string enum mapping.

## C08 — `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Queries.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Queries.cs) · **Coverage:** fresh full text.

Aggregate loading, null-selection fallback, unread totals and query scaling debt.

## C09 — `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs) · **Coverage:** fresh full text.

Real write/save boundaries, unread behavior, ambient transaction and post-save notification.

## C10 — `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Support.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Support.cs) · **Coverage:** fresh full text.

Validation, activity failure isolation, direct Changed invocation, manual/reply helpers.

## C11 — `src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj) · **Coverage:** fresh full text.

Razor net10 project with Infrastructure, SharedKernel and BaseLib dependencies.

## C12 — `src/Modules/CanDoItAll.Modules.Collaboration/README.md`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.Collaboration/README.md) · **Coverage:** fresh full text.

Already bounded DbContext; canonical profile and non-enforced concurrency-token semantics.

## C13 — `tests/Integration/CanDoItAll.Tests.Integration/CollaborationIntegrationTests.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/tests/Integration/CanDoItAll.Tests.Integration/CollaborationIntegrationTests.cs) · **Coverage:** fresh full text; source-count only, no test discovery.

Four facts: owner/schema mappings, restart/profile isolation, create/read-state, automation ingress.

## C14 — `tests/Components/CanDoItAll.Tests.Components/MainLayoutCollaborationTests.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/tests/Components/CanDoItAll.Tests.Components/MainLayoutCollaborationTests.cs) · **Coverage:** fresh full text; source-count only, no test discovery.

One fact verifies MainLayout badge with real owner/component harness.

## C15 — `src/App/CanDoItAll.Web/Components/Layout/MainLayout.State.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/App/CanDoItAll.Web/Components/Layout/MainLayout.State.cs) · **Coverage:** fresh full text.

Service Changed subscription, initial shell load, disposal unsubscribe.

## C16 — `src/App/CanDoItAll.Web/Components/Layout/MainLayout.DatabaseProfiles.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/App/CanDoItAll.Web/Components/Layout/MainLayout.DatabaseProfiles.cs) · **Coverage:** fresh full text in ranges 1-95 and 96 through EOF.

Actual Collaboration badge read method and database-profile host context; event-dispatch call chain must still be traced during execution.

## C17 — `src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor) · **Coverage:** fresh excerpt lines 1-150 only.

Candidate comparison: real project and party integration, filters and structured editor.

## C18 — `src/Modules/CanDoItAll.Modules.Memory`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/git/trees/361e191f5e7a39e0f40e87c14aa981e0ca50567f?recursive=1) · **Coverage:** fresh recursive tree response excerpt; truncated, no provider source audit or exhaustive count.

Broader provider/profile/capability/transport UI family visible inside implementation module.

## C19 — `src/UI`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/contents/src/UI?ref=7db3543ab437376baeca55089cb331fbe1b30483) · **Coverage:** fresh directory names/metadata; no whole-subtree source audit.

Existing feature UI/library names; Collaboration UI absent from this directory at review.

## C20 — `AGENTS.md`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/AGENTS.md) · **Coverage:** fresh full text.

Required engineering/UI/testing authority and portability closure.

## C21 — `.github/copilot-instructions.md`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/.github/copilot-instructions.md) · **Coverage:** fresh full text.

Components MCP, no Radzen, ownership, coding style, targeted tests and current gates.

## C22 — `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/CanDoItAll.CrmHr.UiSandbox.csproj) · **Coverage:** fresh full text.

Light UI reference, template exclusion, Parity/Fast assets and generated-theme checks.

## C23 — `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/Program.cs`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/src/Sandboxes/CanDoItAll.CrmHr.UiSandbox/Program.cs) · **Coverage:** fresh full text.

Interactive backend-free composition pattern; feature-specific chart/read-port registrations are not universal requirements.

## C24 — `docs/testing.md`

[Source](https://github.com/fyziktom/CanDoItAll/blob/7db3543ab437376baeca55089cb331fbe1b30483/docs/testing.md) · **Coverage:** fresh lines 1-190; wider testing/CI/portability evidence inherited from shared S03/S06/S19 at same revision.

Test solutions, isolated PostgreSQL 18, exact discovery, owning project builds, bUnit async rules.

## C25 — `shared/audit/source-register.json`

[Source](shared/audit/source-register.json) · **Coverage:** supplied shared v3 retained unchanged; inherited same-revision audit, not a fresh re-read of every source.

Remaining module metadata S26-S35, backend-only S32/S37, MAF UI S17/S18, common graph/CI/assets conventions.

## C26 — `Microsoft Learn: dotnet watch`

[Source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch) · **Coverage:** official documentation read on preparation date; CLI options and watched project graph.

Watch scans project references; --list enumerates discovered files; distinguish Hot Reload and restarts.

## C27 — `src/Modules/CanDoItAll.Modules.TestLab/Pages`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/git/trees/1bed42d93e39e854c2b0251fb55ae5b6f655c4f3?recursive=1) · **Coverage:** fresh complete Pages tree; parent directory metadata excerpt includes committed-save/projection type names, not bodies.

One TestLab page; parent module includes related save/projection implementation types.

[Parent directory metadata](https://api.github.com/repos/fyziktom/CanDoItAll/contents/src/Modules/CanDoItAll.Modules.TestLab?ref=7db3543ab437376baeca55089cb331fbe1b30483).

## C28 — `src/Sandboxes`

[Source](https://api.github.com/repos/fyziktom/CanDoItAll/contents/src/Sandboxes?ref=7db3543ab437376baeca55089cb331fbe1b30483) · **Coverage:** fresh complete directory listing.

Three sandbox directories: AgentFramework, CrmHr and Prompts. No Collaboration sandbox at this review path.

## Shared-input integrity

The supplied `CanDoItAll_UI_Decoupling_Shared_v3.zip` has SHA-256:

```text
0fad1e632cfaf7a812783be3789cb4af1a708f8eb8288e6afde86e155eff8e1a
```

Its bundle contents are retained byte-for-byte in `shared/`; the original ZIP container is not nested into this archive. The preserved shared manifest and root package manifest serve different scopes. The shared package's historical validation report is not a new application test result.

## Execution-time authority

Re-read current `AGENTS.md`, engineering instructions, canonical seams guidance, testing/CI and the actual changed symbols on the execution checkout. The prepared paths and observations guide discovery, not checkout or code-generation commands. Required type/route consumers and tests must be discovered again if source changed. A branch moving after review does not authorize resetting it.
