# Navigation state

How a routed page makes its meaningful state addressable: what belongs in the URL, how it is
written, how history behaves and how dialogs stay dialogs while still being bookmarkable.
This is the canonical guidance for every routed workspace. The feature-neutral primitives live
in [CanDoItAll.AppComponents.Navigation](../../src/UI/CanDoItAll.AppComponents.Navigation/README.md);
every vocabulary — keys, tokens, defaults, dependency rules — belongs to the module that owns
the page.

## Decisions

- The URL is the authoritative description of shareable navigation state. A refresh, a copied
  link and a Workbench restore reproduce the same semantic context.
- Route identity does not dictate presentation. An addressable detail can stay a dialog: the
  agent editor is a dialog whose agent and section come from the URL.
- The routed page owns the URL. Children receive typed values and raise typed intents; they
  never parse or build their page's URL and never call `NavigationManager` for its state.
  Real cross-page links are the only exception.
- One composition — state record, codec, binding — instead of a page base class. Each page keeps
  its own load lifetime, fences and dialogs.
- Applied filters go into the URL; drafts never do.
- An explicit identity in the URL wins over a default filter or a remembered preference:
  `URL > persisted preference > product default`.

## What goes where

| State | Where | Examples |
|---|---|---|
| Primary scope or resource | Path | `/projects/{projectId}/structure` |
| Peer area inside one workspace | Query `tab` | `/agents?tab=providers` |
| Selected record or route-driven overlay | Query `<entity>Id` | `agentId`, `partyId`, `runId` |
| Section of the selected record or overlay | Query `section`, or `<entity>Section` when a page has several | `agentSection=runtime` |
| Overlay without its own identity | Query `dialog` | `dialog=shared-connections` |
| Committed search, filters, sort, page, date, view | Query | `q`, `status`, `page`, `date`, `view` |
| Personal preference | Profile or browser storage | page size, compact mode |
| Geometry and transient UI | Component memory | pane width, zoom, hover, toasts, spinners |
| Drafts, validation, request generations | Editor and read sessions | unsaved fields |
| Secrets, tokens, prompt or response content, personal free text | Never in navigation state | API keys, subject, issuer |
| Scenario and test switches | Development sandboxes only | `mockScenario` |

A separate route is right for a peer work area with its own load lifetime; a query value is right
for a view inside the same workspace. Do not add a route per subtab that shares one editor, one
data set and one dirty-state boundary.

## Vocabulary

- Keys are camelCase and stable: `tab`, `section`, `dialog`, `q`, `page`, and `<entity>Id`.
  Keep an existing public key when it already has that meaning.
- Every enumerated value has an explicit `UrlTokenMap<T>`: lowercase kebab-case tokens, never
  `enum.ToString()`, a localized label or a numeric index. A renderer's index is a render detail
  derived from the typed value.
- Identifiers use GUID `D` format; dates `yyyy-MM-dd`; instants UTC `yyyy-MM-ddTHH:mm:ssZ`;
  pages are one-based; several tokens are comma-separated.
- Defaults are omitted: the default tab, `page=1`, `false` flags, empty text.
- Old tokens and keys stay readable through `WithAliases` or explicit codec fallbacks and are
  rewritten once with Replace.

## Codec contract

A page's state is an immutable record. Its `IUrlStateCodec<TState>`:

- declares every key the page owns in `Keys`;
- `Read` is tolerant: an invalid value is dropped, never thrown, and dependency rules are
  applied — a section without its record, a record on a tab that does not use it, a page
  without its list;
- `Write` emits only canonical values, defaults omitted, in a fixed order;
- reading the written form returns the same state, and writing a read state twice is stable.

`UrlLocation.Compose` replaces only the owned keys. Foreign keys keep their original text and
the fragment is preserved, so two independent owners on one URL do not erase each other.

## Binding a page

```csharp
[SupplyParameterFromQuery(Name = ThingsUrlCodec.TabKey)] public string? TabQuery { get; set; }
[SupplyParameterFromQuery(Name = ThingsUrlCodec.ThingKey)] public string? ThingQuery { get; set; }

protected override void OnInitialized()
    => url = new(Navigation, ThingsUrlCodec.Instance, History, typeof(ThingsPage));

protected override async Task OnParametersSetAsync() {
    var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
    if (change.IsChanged) {
        await ApplyAsync(change.Previous, change.Current);
    }
}

private Task SelectThingAsync(Guid id) {
    url.OpenOverlay(url.State with { ThingId = id });
    return ApplyAsync(previous, url.State);
}
```

- Declare a `[SupplyParameterFromQuery]` trigger of type `string?` for every owned key, named by
  the codec's key constant. The binding reads the complete location, so the page never parses the
  trigger values, and a malformed value cannot throw during parameter binding.
- `Apply` runs in `OnParametersSet`, never only in `OnInitialized`: the same page instance
  receives query-only changes, Back and Forward.
- `Apply` canonicalizes with one Replace when the location is not canonical. Pass
  `RendererInfo.IsInteractive` so prerendering never redirects. The same flag gates every write:
  while it is false an intent updates `url.State` but never turns into a server redirect.
- Pass the page type (`typeof(ThingsPage)`, not `GetType()`, so test subclasses keep the route).
  The binding then ignores a location outside the page's `@page` routes (and the location it was
  created at) and never composes its keys onto it, so a page that is still alive after leaving
  cannot rewrite the next page's URL.
- An intent updates `url.State` immediately; the location follows. The binding ignores its own
  in-flight and superseded locations, so rapid intents never flicker back.
- `change.Previous` and `change.Current` tell the page what to reload: a section change must not
  reload a catalog, a filter change reloads only its list. Keep the existing request generation
  or cancellation fence for every URL-dependent read.
- Derive such a fence from the applied state, never from `NavigationManager.Uri`: on Blazor
  Server the location of a page-originated write arrives after a browser round trip, so a fence
  computed from the raw location changes in the middle of the read the intent started.
- A child that publishes a value which its host feeds back keeps the published value until the
  host's next parameters arrive. A queued render of the child can run before the host's render
  reaches it, and handing the stale parameter to a controlled grandchild repeats or resets its read.
- A write renders the page while the load its intent started is still running, so the user can
  open another tab inside that window. A load started by an intent finishes by preparing what the
  active tab needs, as a route load does.
- A child applies a location value when that value changes or the location names another record,
  never on a mere render. A draft has no record in its URL and so no section there either; the
  section it is on lives in the editor until the save publishes identity and section together.

## History

| Action | History |
|---|---|
| Select a peer tab or area | Push |
| Select a record, open a route-driven overlay | Push (`OpenOverlay`) |
| Apply a filter or search with an explicit action, change page, sort, date or view | Push |
| Commit of a live (debounced) search or an instant client-side filter | Replace |
| Change a section of an open overlay | Replace |
| Change a section of an inline editor | Push |
| Selection published by a child that may choose a default by itself | Replace when the URL had no selection, Push when it replaces another selection |
| Start a new draft of a record | Push the location without the record; saving it then publishes the new identity with Replace |
| Typing in a search box, debounce intermediates | No navigation |
| Canonicalization, defaults, legacy tokens, invalid values | Replace (automatic) |
| Compatibility redirect | Replace |
| A record disappeared or was deleted while open | Replace |

One user action creates at most one history entry.

## Services

`AddAppNavigation` registers `IAppNavigationHistory` with the runtime modules. A page with
route-driven overlays injects it and passes it to its binding; a native host registers its own
implementation of the same seam.

The Router turns on client-side navigation only after its first render. Before that,
blazor.web.js turns a `NavigateTo` into an enhanced page load: the whole page is fetched and
rendered again, and a later navigation aborts it. Pages write while they become interactive (a
canonical form, a default selection), so the Web app's `Routes` turns navigation interception on
when the circuit starts. Keep that call in any host that renders these pages.

## Route-driven overlays

- The overlay is open exactly when its identity is in the URL. Render it declaratively with the
  BaseLib `Dialog` (`IsOpen` from state, `OnClose` raising the close intent) or reconcile it from
  the state; never open it imperatively and record the URL afterwards.
- `OpenOverlay` pushes the overlay location and marks the entry with its parent.
  `CloseOverlayAsync(parent)` goes Back when the entry carries that parent, so Back and Close
  have the same effect and Forward reopens the overlay. A deep-linked overlay has no marker:
  Close replaces the location with the parent.
- The Back of a Close runs after a browser round trip, and a browser runs a Back relative to the
  entry it was requested on, dropping an entry pushed before it runs. While its own Back is in
  flight the binding therefore holds later writes and sends them once the Back has arrived; a
  location that arrives first supersedes them. A browser test that leaves the page right after a
  Close waits for the location to leave the overlay first, or the late Back aborts its navigation.
- Sections inside an overlay use Replace and keep the marker.
- A reload keeps the marker in the browser entry, but Blazor starts the circuit without
  `HistoryEntryState`. The binding asks `IAppNavigationHistory.GetEntryStateAsync` once for the
  entry it starts on, so a reloaded overlay keeps its marker on Replace and still closes with Back.
  A host that cannot tell returns null, and Close falls back to Replace.
- A host that turns an uncontrolled child into a route-controlled one opens the overlay for every
  newly requested identity. Markers the child kept to ignore echoes of its own selection must not
  suppress it: a browser delivers click before dblclick, so the selection always comes first.
- Confirmations, wizards and other transient dialogs stay on `DialogService` and outside the URL.
- A deep-linked overlay must render its loading, unavailable and forbidden states itself; an
  unknown identity is reported as unavailable and is not silently replaced by another record.

## Children and renderers

- Renderers keep emitting the intents they already have; the routed host translates them into
  state transitions. Do not add URL knowledge to `src/UI` renderers.
- A child that selects, filters, pages or opens an overlay exposes the value as a parameter and
  the change as a callback. A child that loads a list exposes its committed query, not its draft.
- A child that must know a page's URL state receives it as typed parameters, never by reading
  `NavigationManager.Uri`.
- A paged catalog is controlled: the page passes the committed query and the catalog reports
  each change with its kind. A live search commit and a page correction Replace; tags, filters,
  reset and paging Push.
- A presentation model whose dependencies may not include this library reports its own
  navigation kind (for example the Scheduler's), and the page maps it to `UrlHistoryMode`.

## Links between modules

A link to another module's page is a cross-page navigation, not a state change. Build it from
the target owner's published route or link builder when one exists, otherwise from the target
route and its documented keys. Never write another module's internal state keys from guesswork;
when a link must carry state, the target module owns the builder.

## Workbench

A Workbench tab is resolved from the path; a path that embeds its scope (`/projects/{id}/structure`)
gives one tab per scope. Its restore route is the latest location of that tab, so every query
change on the page updates the tab instead of creating another one.

A query identity opens a dedicated artifact tab (`?projectId=` on `/projects`, `?promptId=`,
`?planId=`) only on an explicit command: the navigation carries
`WorkbenchNavigation.OpenTabHistoryState` as its history entry state. Selecting, closing or
reopening a Workbench tab uses the same marker. In-page selection never multiplies tabs.

## Security

Every identifier from a URL is untrusted input and is authorized by its owner. The URL is never
proof of access. Not-found and forbidden states reveal nothing about another tenant's records.
Never write secrets, tokens, prompt or response content, personal free text or drafts into
navigation state; URLs end up in history, logs and proxies.

## Page vocabularies

Each routed page's codec is the authoritative vocabulary; this index says where to find it.
Defaults are omitted from every URL.

| Route | Codec | Keys |
|---|---|---|
| `/agents` | `AgentsUrlCodec` (AgentFramework module) | `tab`; `agentId` + `agentSection` (editor dialog on `agents`, selection on `chat`, `capabilities`, `governance`); `teamId`; `simpleChatView`, `definitionId`, `conversationId`; `usageScope`, `usagePeriod` (overview); `providerId`, `providerSection`, `dialog=shared-connections`; history `range`, `to`, `from`, filters, `size`, `cursor` + `page`, `entryId` |
| `/agents/workflows` | `WorkflowsUrlCodec` (AgentFramework module) | `tab` (History is the default when `runId` is present), `projectId`, `workflowId`, `runId`, `dialog=run-details` |
| `/collaboration` | `CollaborationUrlCodec` (Collaboration module) | `view`, `unread`, `threadId` |
| `/crm-hr/directory` | `DirectoryUrlCodec` (CRM / HR module) | `partyId`, `dialog=new-party`, `section`; catalog `q`, `tags`, `scope`, `page` |
| `/crm-hr/crm` | `CrmUrlCodec` (CRM / HR module) | `accountId`, `opportunityId`, `interactionId`, `section`; catalog `q`, `tags`, `page` |
| `/crm-hr/workforce` | `WorkforceUrlCodec` (CRM / HR module) | `partyId`, `section`; catalog `q`, `kind`, `page` |
| `/crm-hr/recruiting` | `RecruitingUrlCodec` (CRM / HR module) | `applicationId` (a `partyId` resolves to it), `dialog=new-application`, `section`; catalog `q`, `stage`, `page` |
| `/crm-hr/agents` | `CrmHrAgentsUrlCodec` (CRM / HR module) | `partyId`, `section`; catalog `q`, `validation`, `page` |
| `/crm-hr/assignments` | `AssignmentsUrlCodec` (CRM / HR module) | `projectId`, `tab`, `q`, `role`, `status`, `page` |
| `/processes`, `/projects/{id}/processes` | `ProcessWorkspaceUrlCodec` (Processes module) | `tab` (Runs is the default with a `runId` or `launchPlanId`), `definitionKey`, `q`, `scope`, `runId`, `runView`, `window`, `chatWindow`, `page`, `dialog=run`, `eventId`; legacy `processId`, `launchPlanId` |
| `/processes/live`, `/projects/{id}/processes/live` | `LiveProcessesUrlCodec` (Processes module) | `tab`, `runId`, `status`, `window`, `page`, `dialog=run\|files`, `stepId`; `processStarted` is read once and removed |
| `/scheduler` | `SchedulerUrlCodec` (Scheduler module) | `tab`, `planId` (editor dialog), `view` + `date` (calendar), `q`, `kind`, `state` (schedules), `historyQ`, `historyStatus`, `historyKind` |
| `/test-lab` | `TestLabUrlCodec` (TestLab module) | `planId`, `section`, `projectId` (draft seed), `q`, `project`, `phase`, `result` |
| `/projects` | `ProjectsUrlCodec` (Projects module) | `projectId` + `mode` + `step`, `hierarchyId`, `filesId` (one overlay at a time), `q`, `status`, `partyCategory`, `party`, `parentId`, `scope`, `subprojects=false`, `view=files` |
| `/projects/{id}/structure` | `ProjectStructureUrlCodec` (Workbench module) | `tab` (`gantt`, `manager-summary`) |
| `/projects/{id}/calendar` | `ProjectCalendarUrlCodec` (Workbench module) | `view`, `date`, `eventId`; URL over the saved preference over the default |
| `/resources` | `ResourcesUrlCodec` (Resources module) | `view=browse`, `resourceId`, `projectId` (draft seed), `q`, `project`, `connector`, `validation`, `source` |
| `/prompt-gallery` | `PromptGalleryUrlCodec` (Prompts module) | `promptId` (editor overlay), `q`, `tags`, `kind`, `status`, `provider`, `model`, `favorites`, `archived`, `chatModel=false`, `page` |
| `/settings` | `SettingsUrlCodec` (Workspace module) | `tab`; `secretId`, `profileId`, `storageId` on their tabs; `q` + `page` on API access |
| `/plugins` | `PluginsUrlCodec` (Plugins presentation) | `pluginId`, `section`, `logs=all`, `dialog=packages`; the OAuth return keys are read once and removed |
| `/memory` | `MemoryUrlCodec` (Memory presentation) | `providerId`, `tab` |

Provider request history carries an absolute `to` anchor: its server cursor is bound to the exact
interval, so a link with `cursor` reproduces the same page, and a link without `to` is anchored
once with Replace when the page becomes interactive.

## Proof

| Layer | Proves |
|---|---|
| Unit | Codec round trip, canonical form, legacy aliases, dependency rules, history rules of the binding, and that renderers and backend layers never reference the library (`NavigationStateDependencyTests`) |
| Components (bUnit) | The page applies the location, a user intent writes the right URL with the right Push or Replace, overlays open from a deep link and close by intent |
| Playwright | Pasted URL, refresh, Back and Forward across tabs, records and overlays on the real host |

Update a test that asserted the old Replace behavior to the new history rule instead of
weakening it.
