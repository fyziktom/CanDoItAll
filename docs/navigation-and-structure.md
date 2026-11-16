# Navigation and structure — current issues

Working notes for a navigation and information-structure redesign. This document records
the current behaviour first; proposed routes are deliberately separated from it. It is
expected to grow as more observations are collected.

## Projects

### Current behaviour

The project portfolio can hold two different scopes at the same time:

- The project filter selects **many** projects (for example, a project and its
  descendants/related projects).
- The hierarchy tree always has one selected project. Selecting an item in the tree
  applies a single-project (**one**) filter.

This makes it hard to tell whether the current context is a portfolio, a hierarchy
selection, or both.

Files have to bridge this mismatch. The portfolio files view receives the many-project
scope and introduces a select box to reduce it to one project. Other file access is a
dialog opened from an icon button. Thus the same resource has two different homes and
two different scope models.

Other icon buttons lead to a mix of destinations:

- a page with three tabs;
- a calendar page;
- a details dialog;
- a hierarchy dialog.

The result is that actions concerning one project are split between dialogs, unrelated
pages, and tabbed pages without a stable project-level navigation model.

### Proposed project-level structure

Make a selected project a first-class route context. The following six mutually linked
pages should share the same `<SecondaryTabs>` navigation:

| Route | Responsibility | Replaces or consolidates |
|---|---|---|
| `/projects/{projectId}` | Project details | Both current project-detail dialogs |
| `/projects/{projectId}/structure` | Project structure | Existing structure surface |
| `/projects/{projectId}/gantt` | Plan and Gantt | The current planning/Gantt destination |
| `/projects/{projectId}/files` | Project files | Both current file views |
| `/projects/{projectId}/management` | Project management | Management/dashboard-related actions |
| `/projects/{projectId}/processes` | Project processes | Existing project-processes surface |

The portfolio at `/projects` should remain the place for a many-project filter. Opening
an individual project should establish exactly one `projectId`; every page above then
uses that same scope. If a user needs a different project, they return to the portfolio
or use a project switcher, rather than each feature introducing its own selector.

## Collaboration

### Current behaviour

Collaboration resembles a two-panel email client: the left panel lists items and
contains the new-message form, while the right panel displays the selected thread and
its replies. The reply location is conventional (under the message thread on the right),
but creation lives inside the Inbox list pane.

The top tabs—Inbox, Threads, and Escalations—do not merely switch content. They also act
as filters over overlapping data. Inbox and Threads also expose their own filtering
controls. In particular, **Show all** and **Unread only** in Inbox affect escalations as
well.

The URL carries only the selected item, for example:

```text
/collaboration?threadId=c53f1851-869d-40e5-98bc-255807c95ba2
```

It does not represent the selected tab or filters. A previously selected message/thread
can also remain active when the current tab and filters remove it from every visible
list. The URL, selected state, list contents, and tab semantics can therefore disagree.

### Issues to resolve

- Decide whether the three labels are true content sections or views/filters of a shared
  list; their current behaviour is the latter, but their presentation suggests the former.
- Give the compose action a stable home that does not make the Inbox list serve two
  unrelated responsibilities.
- Define selection validity: a selected thread should either remain intentionally visible
  as contextual detail, or be cleared/updated when it is outside the active result set.
- Decide which view state belongs in the URL so a copied or refreshed link reproduces
  the workspace predictably.

## Agents

### Current behaviour

The catalog is reached through `/agents?tab=agents`.

Its upper toolbar contains search plus **New team** and **New agent**. The agent action
uses a plus icon and the team action a people icon. The main surface is then a two-panel
view: a team tree on the left and agent cards on the right. The left panel has another
toolbar containing another **New team** action, this time using a plus icon. The duplicate
action and changed iconography make the creation model ambiguous.

Selection behaves differently in the two panels:

- Selecting an agent in the team tree opens the agent-details dialog.
- Selecting an agent card on the right only marks the card as selected. It provides no
  follow-up action such as details or edit.

The visual selection on the right consequently has no user-facing purpose, while the
same object in the tree is immediately actionable.

### Issues to resolve

- Establish one creation entry point for teams, with one label and icon.
- Make agent selection consistent across the tree and card list: either both open
  details, or both update a purposeful detail/action pane.
- If card selection is retained, expose a clear action that consumes it; otherwise remove
  the selected-card state.
- Consider whether the catalog should remain a query-selected tab or become a stable
  route when its navigation model is redesigned.

### Cross-module links from Agents

The `/agents` toolbar has icon-button links to:

- `/crm-hr/agents`;
- `/agents/workflows`;
- `/processes`.

The relationship to CRM/HR agents needs product clarification. It may be a related agent
surface, or it may be better framed as a contextual/help card: for example, "you can also
use agents in the Workflow agents tab." Its intended user task and relationship to the
main Agents catalog should be agreed with the product owner before retaining it as a
global toolbar action.

## CRM/HR

### Current behaviour

The CRM/HR home (`/crm-hr`) contains a **Module entry points** panel that duplicates
links to the other CRM/HR tabs. Its toolbar also links to the directory. The CRM page
(`/crm-hr/crm`) repeats that directory toolbar link even though Directory is already a
peer in the shared `<SecondaryTabs>` navigation.

### Issue to resolve

The toolbar and in-page entry-point panels should not repeat navigation already supplied
by stable secondary tabs unless the link has an additional, distinct purpose (for example,
opening a pre-filtered task). Decide whether these links should be removed, replaced with
task actions, or moved into contextual guidance.

## Resources

### Current behaviour

Resources is a two-panel filter/list and detail workspace. When no resource exists, a
**New** button appears in both the page toolbar and the left panel. The action resets the
editor to a new resource, but has no visible effect in this state because the new-resource
form is already present in the right panel.

### Issue to resolve

Use an explicit empty detail state instead: when no resource is selected, show a message
and a **Create new resource** action on the right. That action should reveal the form.
The toolbar and list-pane creation actions can then be removed or made genuinely useful
for the selected workflow.

## Cross-cutting issue: duplicate navigation

Duplicated links in toolbars, page panels, secondary tabs, and side navigation appear to
be a recurring pattern. They obscure which controls are navigation and which are actions.

Perform a focused scan of the UI with these questions:

- Does this toolbar button take the user to a destination already available in the
  current `<SecondaryTabs>` or side navigation?
- If yes, does it carry useful additional state, such as a relevant filter, selected
  object, or creation mode?
- If not, remove it or replace it with a task-specific action.
- If it is retained for discoverability, present it as contextual guidance rather than as
  a competing primary navigation control.

## Razor-page audit (2026-09-01)

Static review covered the 28 route-bearing Razor pages and the components that provide
their toolbar actions or secondary navigation. The list below is intentionally a set of
design-review candidates, not a claim that every cross-module link is wrong.

### Confirmed duplicate navigation

| Surface | Observation | Review direction |
|---|---|---|
| CRM/HR home (`/crm-hr`) | The header **Open directory** action and the **Module entry points** panel duplicate destinations already in `CrmHrSecondaryTabs`. | Remove general navigation duplicates or recast the panel as task/context guidance. |
| Resources (`/resources`) | The toolbar and empty list both offer **New resource**, while the right-side editor is already visible in its new-resource state. | Adopt the empty-detail state proposed above; make one action reveal the editor. |
| Test Lab (`/test-lab`) | The toolbar and the empty plan list both offer **New test plan**, while the right-side plan editor is already present. | Apply the same empty-detail-state decision as Resources, or retain only one creation affordance. |
| Settings (`/settings`) | Selecting the **Providers** `SecondaryTabs` item immediately routes to `/agents?tab=providers`. A control presented as an in-page tab is actually cross-module navigation. | Make it an explicit link to the owning Agents/Providers surface, or keep the provider editor inside Settings. Do not make a tab silently change module. |

### Contextual links that currently look like duplicate navigation

The following toolbar actions fall back to the generic Directory tab when nothing is
selected, but pass the selected entity as `partyId` when there is a selection. That makes
them potentially useful deep links, not pure duplicates. Their labels do not communicate
this distinction, however.

| Surface | Current action | Better rule to evaluate |
|---|---|---|
| CRM (`/crm-hr/crm`) | **Open directory** opens the selected account's directory record, otherwise the general Directory page. | Show only with a selection (or disable it) and call it **Open selected account in Directory**. Use the secondary tab for the general Directory destination. |
| Workforce (`/crm-hr/workforce`) | **Open directory** opens the selected workforce party, otherwise the general Directory page. | Show only with a selection (or disable it) and call it **Open selected person in Directory**. |
| CRM/HR Agents (`/crm-hr/agents`) | **Open directory** opens the selected agent party, otherwise the general Directory page. | Show only with a selection (or disable it) and call it **Open selected agent in Directory**. |
| Recruiting (`/crm-hr/recruiting`) | **Open workforce** is shown for a selected application and routes to its workforce party. | Retain as a contextual object link; make its selected-candidate relationship explicit in the label/tooltip. |

### Cross-module toolbar links needing ownership decisions

These links do not duplicate a local secondary tab, but they repeat the same pattern of
global destinations in page toolbars. Their value depends on whether they support the
user's current object/task or simply act as extra global navigation.

| Surface | Link | Question for product/design |
|---|---|---|
| Agents | CRM/HR Agents, Workflows, Processes | Already noted above: decide which relationships are primary work flows and which belong in contextual help/cards. |
| Workflows (`/agents/workflows`) | **Open agents** → `/agents` | The reciprocal Agents ↔ Workflows links create a mini navigation system outside the main shell. Should these be peer navigation, or should the link pass a selected workflow/agent context? |
| Collaboration (`/collaboration`) | **Open scheduler** → `/scheduler` | Is scheduling an action on the selected collaboration thread, or merely a global destination? If global, it belongs in shell navigation rather than the workspace toolbar. |
| Live Processes (`/processes/live` and project-scoped equivalent) | **Definitions** → the matching process-definition route | This is a valid sibling transition, but it exposes Definitions as a toolbar action rather than a stable Processes/Live view model. Consider shared process navigation, especially for project-scoped routes. |

## Processes: run links, filters, and details

### Current behaviour

Recent-process links can point directly to a live-process run, for example:

```text
/processes/live?runId=ddf82973-9196-406f-a0e0-8f2dd44d04ae
```

The destination can nevertheless show no matching run because the dashboard's default
time filter is **last hour** and that filter state is not represented in the link. To find
an older run, the user must currently open `/processes`, select the **Activity** tab,
remove the time filter, and open its run-details dialog. That dialog state is not
bookmarkable.

This is a mixture of three independent concerns:

- a durable `runId` deep link;
- list filtering that can hide the linked record; and
- a modal-only run-details experience.

### Issues to resolve

- A route containing `runId` must resolve and show the requested run regardless of a
  default recency filter. It can either override the filter, add the run as a pinned
  result, or make the time range explicit in the URL.
- Make list/view state that changes whether a deep-linked record is visible reproducible
  in the URL, at least for time range and active Processes/Activity view.
- Give a run a canonical, bookmarkable details route. List cards, recent-process links,
  and activity views should all point there; a dialog may remain as a lightweight preview
  only if it links to that canonical route.

## Cross-cutting issue: modal detail routes

Many current details are opened in dialogs. This is appropriate for short, interruptible
tasks, confirmations, and quick previews. It is a poor fit when a user may need to:

- return to the object later;
- share or bookmark the object;
- navigate among several aspects of the object; or
- preserve a selected object while changing a surrounding list or filter.

For those cases, introduce a canonical page route keyed by the object's durable ID.
Project details and process-run details are immediate candidates. Existing dialogs can
then become previews or editing overlays launched from the page, rather than the only
way to access an object.

### Audit conclusion

The recurring issue is not that a user may navigate from one module to another. It is
that the same visual treatment is used for three different things:

1. global module navigation;
2. peer-section navigation; and
3. a deep link for the currently selected record.

The redesign should reserve toolbar actions for the third category or for real commands.
Use shared tabs/side navigation for the first two. Where a toolbar link is contextual,
hide or disable it without a valid selection and name the target record in the control.
