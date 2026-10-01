# Projects Files P2 — scope and boundary design

## Whole family, not a demonstration fragment

| Surface | Required P2 result |
|---|---|
| Single-project file dialog | Real loading/unavailable/retry, full-screen desktop Dialog, FileBrowser, registered read-only FileInteraction and existing local/download actions |
| Filtered portfolio Files pane | Exact P1 projection, source summary/revision, refresh, search/browser, preview and Back to filtered files, source replacement and empty states |
| Production integration | Both existing user routes reach the new renderers through original owner adapters; P1 board slot and agent-context integration stay functional |
| Independent sandbox | Same actual renderers, neutral in-memory provider/session/content fixtures, lifecycle controls, real static assets and standalone publish |

Do not add file editing, rename, upload, delete, recursive operations, new data sources, new package import/export or new runtime capabilities. Preserve the behavior difference: the portfolio has Back to filtered files; the single-project interaction currently returns by closing/reopening the dialog. Do not silently redesign those flows.

## Dependency direction

Recommended names (adapt only if the current repository already provides a better equivalent):

```text
Projects module / production effect hosts
  -> CanDoItAll.Projects.UI (existing P1; kept light)
  -> CanDoItAll.Projects.Files.UI (new actual Files renderers)
  -> original file coordination / authorization integration

Projects.Files.UiSandbox
  -> Projects.Files.UI
  -> genuine neutral FileBrowser and FileInteraction components/contracts
  -> bounded synthetic source/content adapters
```

Contracts can live in the Files UI assembly or in a small Files.Contracts if useful. Reference the existing Projects.Contracts for the exact shared filter/portfolio values when appropriate. The reverse direction is forbidden: broadly consumed Projects.Contracts must not acquire file integration/rendering dependencies. Foundation, MAF and AppComponents remain unaware of product Files semantics.

The real Projects.UI and its sandbox must not reference Files.UI or the production module. Keep the existing typed `FilesContent` slot and have the original module supply a Files effect host. Do not pull a backend-connected child into a supposedly pure leaf. No copied ProjectFileFilterProjection and no second Cards/Files filtering algorithm (R03, R22, R23, R28).

Reuse the existing FileBrowser and FileInteraction libraries. The wider AppComponents file-action helpers may stay in production hosts. If a genuinely neutral primitive is needed, use its proper existing owner and prove the exact graph; do not add a broad dependency for a single enum or duplicate the helper. Concrete ProjectFilesPilotWorkspace/Interaction and ProjectFilePortfolioWorkspace carry ownership/integration and must not simply be moved wholesale into a public render contract (R13-R21).

## Responsibility allocation

The route owns navigation, saved portfolio filters and contextual agent publication. Each file effect host owns its original target, captured profile/actor, accepted source/workspace and independent preview lease, request generations and external actions. The renderer owns visual composition, neutral browser binding, element references and intrinsic focus. Concrete grant resolution, actual source providers and file access remain in Integration/Storage.

One controller may serve both surfaces if it truly models common policy without mode flags proliferating into an application-wide manager. Two cohesive hosts with a small shared lifecycle helper are also valid. Choose the smallest maintainable form; the package does not prescribe partial-class counts or an interface quota.

No persisted schema or protocol change is needed for the extraction. Retain existing endpoint contracts and semantic scopes. A render snapshot is not current authorization. Keep backend checks for current profile, actor, source membership and permitted operation, including stale/removed scope controls.

## Assets and build wiring

Move component-scoped styles, any feature JS, Tailwind inputs and static web asset paths with their actual owner. Reuse proper FileTools registrations and all already supported viewer renderers. Do not relabel an unsupported viewer as successful or substitute plain text for a real PDF/image renderer to pass the proof.

Wire new tests into owning Components/Stable solutions and applicable CI selections; keep test projects outside the product solution. Evaluate actual project/package closures with sibling source mode and after publish. Standalone Files sandbox must not start PostgreSQL, vault, background provider services, inference or native launch to render.

## Completion census

Current Projects Pages/Components contains the two Files components and ProjectsAgentChatContextProvider; ProjectsPage and its read-generation helper remain outside that folder. After the change, document which components became renderers and which are legitimate route/effect/context hosts. Inspect the actual current consumers rather than assuming this three-file census is exhaustive forever.

Do not declare the entire Workbench/Project Structure family complete because Projects P2 is complete. Do not start the remaining AgentFramework or Workflow authoring work in this run.
