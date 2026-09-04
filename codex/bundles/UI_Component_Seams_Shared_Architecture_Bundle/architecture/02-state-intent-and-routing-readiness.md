# State, intent, and routing readiness

## Why state ownership is part of component refactoring

Later bookmarkability work requires a stable semantic state model. If route-significant
state remains hidden inside child components, adding URL codecs will force another
component refactor. Therefore component seam bundles must classify and relocate state
ownership now, while actual URL binding remains out of scope.

## State taxonomy

| State category | Intended owner/storage | Examples |
|---|---|---|
| Primary identity | Page/workspace state; later path/query | `agentId`, `providerId`, `projectId`, `runId` |
| Shareable view | Page/workspace state; later query | section, committed filter, sort, page |
| User preference | Profile or local preference store | default page size, compact mode |
| Workspace geometry | Workbench/local state | floating window position, pane width, canvas zoom |
| Transient presentation | Component memory | hover, dropdown, spinner, toast |
| Draft editor/filter | Editor or draft state | unsaved values, uncommitted search text |
| One-shot feedback | Host/history/flash state | saved banner, process started |
| Sensitive data | Never shared location state | secrets, API keys, confidential payloads |
| Development scenario | Sandbox-only input | mock scenario key |

## Ownership rule

A routable page or feature workspace owns the state that describes the semantic location.
Child components:

- receive typed state or individual controlled values;
- receive precomputed cross-page links when needed;
- emit typed callbacks or intents;
- do not parse the page query;
- do not construct the parent page URL;
- do not call `NavigationManager` to mutate parent workspace state.

A child may still navigate to a genuinely different feature/page when that is an explicit
cross-page link rather than mutation of its parent state.

## Transitional pattern before routing exists

The state owner can remain in memory first:

```text
child intent
    -> page/workspace reducer or handler
    -> updated typed state
    -> existing DialogService or local rendering
```

Later routing changes only the outer transition:

```text
child intent
    -> page/workspace reducer
    -> URL navigator
    -> parsed typed state
    -> same component rendering
```

This allows component untangling and bookmarkability to progress in separate bundles
without designing the state twice.

## Controlled state versus local state

Make state controlled when it:

- identifies a durable object;
- chooses a meaningful workspace section;
- opens a significant detail/overlay;
- affects Back/Forward expectations;
- should survive refresh or Workbench restore;
- controls a committed filter, sort, or page.

Keep state local when it:

- is a draft;
- is transient;
- has no meaning outside the component;
- is geometry or animation state;
- would create excessive navigation noise;
- contains sensitive values.

## Typed callbacks or typed intents

Use a simple `EventCallback<T>` when the component has one obvious state transition:

```text
SelectedAgentIdChanged
SearchCommitted
PageChanged
```

Use a small typed intent union when several related actions share one state machine:

```text
SelectAgent
SelectTeam
OpenAgentDetails
CreateAgent
OpenManagedChat
```

Do not introduce an intent hierarchy merely to wrap one callback. Do not expose dozens of
loosely related callbacks when one coherent intent model makes ownership clearer.

## Stable identity

- Use semantic enums/keys inside the feature state.
- Do not treat a numeric tab index as a durable identity.
- Map future URL tokens explicitly; do not expose localized labels or `enum.ToString()` as
  public contracts.
- Visual presentation is independent from route identity. The same state may render as a
  page, dialog, side sheet, Workbench tab, or MAUI overlay.

## Route-ready decision

A result is route-ready when:

- all shareable state has one page/workspace owner;
- the state is representable as immutable typed data;
- child components are controlled for route-significant state;
- UI actions emit deterministic transitions;
- the model excludes draft, geometry, and sensitive data;
- binding a codec/navigator does not require changing feature component ownership again.
