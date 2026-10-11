# Remaining scope and finite completion boundary

The review found five concrete presentation clusters in the Web assembly. It did not prove
that these are the only remaining active edges in the entire repository. CD1 closes that gap
by completing the current census and its real residuals, not by declaring the seed exhaustive.

## Required application clusters

| Cluster | Current source | Required result |
|---|---|---|
| Dashboard | Web `Components/Pages/Home.razor`, `Dashboard/DashboardModels.cs` | Independent operational dashboard renderer; native snapshot/cache/timer owner retained. |
| Runtime Capabilities | Web `Components/Pages/RuntimeCapabilities.razor`, `CapabilityFact.razor` | Independent complete readiness presentation without a Composition dependency. |
| Database dialog | Web `Components/Layout/MainLayoutDatabaseDialog.razor` | Safe presentation contract; native profile models/authority remain outside the renderer. |
| Utilities/database flyout | Web `Components/Layout/MainLayout.razor`, especially `BottomUtilities` | Real reusable utilities/flyout presentation with origin-bound callbacks; native startup and effect composition retained. |
| Status bar | Web `Components/Layout/MainLayoutTopBar.razor` | Reuse the already presentational shape in a suitable light library; no new controller simply to move a view. |

See [APPLICATION_SURFACES.md](APPLICATION_SURFACES.md) for functional requirements.
The concrete paths and source hashes are in [inventory-seed.json](inventory-seed.json).

## Existing product families: preserve, verify by impact, do not re-extract

The observed `src/UI` directory has 36 project directories. This count is inventory, not a
completion percentage. The [family register](family-register.json) lists every observed name
and its review confidence. Confirm current evaluated projects, linked files and active usages.

| Family | Existing boundary / closure treatment |
|---|---|
| Processes | PC1 UI plus PC2–PC4 correctness and native authoring. Review found the PC4 repairs wired; preserve and rerun touched paths. |
| Workbench | Structure, Planning, Operators, Content, Insights and Execution. WB6 lists every native entry and zero unfinished active renderers. Its historical validation status requires evidence reconciliation, not another extraction. |
| Agents | Workspace, editor, providers/sharing, capability/team authoring and workflow authoring. AC1 records scoped completion and a renderer census. Use it as a source-addressed starting point, including actual avatar/dialog/chat descendants. |
| Workspace | Core settings, API Access, Data Sources, storage catalog/recovery/selection, and neutral configuration fields. Existing leaves are not fresh proof of every behavior; classify active host/deferred boundaries. |
| Projects and Files | Existing portfolio and file leaves. Keep admissions, selection, file scopes and effect outcomes with their native owners. |
| CRM/HR, Prompts, Collaboration | Existing leaves and scenario hosts; preserve editors, drafts, native reads/writes and cross-feature selections. |
| Resources, Memory, SchedulerPlanner, Plugins, TestLab | Existing leaves and owning native services. Reconcile their maintained evidence and touch only real residuals or affected consumers. |
| AppComponents, conversations, record browsing, embedded browser | Existing shared presentation. Inspect actual contributor/child closures and assets; generic composition is not automatically a forbidden edge. |
| Components.Git | Observed project contains a marker and project reference, not a renderer. Do not manufacture a Git UI extraction or delete compatibility solely to reach zero files. |

## Legitimate retained native code

Native routed hosts, mutation/query adapters, authorization, profile activation, context and
conversation contributors, lifecycle coordinators and the Web composition root remain native.
Framework App/Routes, reconnect/reload/error/not-found plumbing may remain Web-owned with
explicit reachability and responsibility rationale. Do not use a line-count target or empty
Razor directory as the definition of success.

Unreachable public/legacy compatibility components may remain only with a current search of
routes, registrations, dynamic activation, tests and supported extension contracts. A missing
lexical caller alone is insufficient. Do not delete dormant public APIs without authorization.

## Remaining work that is not a components-decoupling implementation requirement

Record, do not silently implement: authoring-history transfer/export, large catalog paging,
future backend/module ownership and concurrency refactoring, new authentication/transport,
new runtime capabilities and live-provider/demo certifications. Keep existing refusal and
safety behavior intact. Report known operational cleanup restrictions separately.

## Scope lock without an audit-only escape

At S1, name every actual residual, its owner, impact and proof. Freeze the closure queue for
this task and work it to zero. A newly discovered **in-scope** active rendering violation is
added and completed in the same assignment. An unrelated enhancement goes into the separate
handoff backlog with reason and owner, not into an endless new task queue.

Do not stop after making the census, the dashboard, the first sandbox, or one commit. Conversely,
do not keep refactoring after the closure conditions are met simply to generate another bundle.
