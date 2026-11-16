# Side nav organization: brainstorm

## Purpose

This is a brainstorm, not a decision record. It looks at how the *menu structure itself*
(not the URLs) could be grouped, given the domain vocabulary in
[domain-model.md](design/domain-model.md) and the canonical-route proposal in
[navigation-proposal.md](navigation-proposal.md). Companion piece: that document fixes
*where things live* (URLs); this one is about *how the side nav presents them*.

## Current state (as implemented)

From reading `AppShell.razor`, `MainLayout.razor`, `MainLayout.Workbench.cs`, and
`ShellNavigation.cs`:

- There is one scrollable nav list, not an actual top/bottom split: Dashboard,
  Projects, Collaboration, CRM/HR, Agents, Resources, Plugins, Prompt Gallery, Test
  Lab, Scheduler, Settings. When it doesn't fit the sidebar height, items past a
  computed capacity spill into a "More" popover, with the active route force-pinned
  into the visible set.
- A separate fixed `BottomUtilities` slot (visually "the bottom") holds Chats,
  Settings, Runtime capabilities, and a database flyout. This is why Settings appears
  twice: once as a normal nav item at `/settings`, once as a bottom-utility button whose
  handler just navigates to the same `/settings` route. Two entry points, no second
  meaning.
- "Opened" is a merge of opened projects + opened prompt/workbench sessions into one
  flyout with a badge count — effectively an ad hoc recency drawer bolted onto the nav,
  not a place in the information architecture.
- The database flyout shows the current DB profile and recent databases, with a button
  that opens the full switch-database dialog. As implemented it mostly duplicates what
  that dialog already does — it doesn't add a distinct action of its own, which matches
  your read that it's "basically doing nothing."

Four independent problems, worth keeping separate even though one restructuring can fix
all four: (1) an unprincipled flat item list that forces mechanical overflow, (2)
Settings with two unrelated entry points, (3) "Opened" as a bolted-on flyout instead of
a first-class place, (4) a DB switcher that adds a hover step in front of a dialog it
doesn't change.

## What the domain model already suggests

`domain-model.md`'s second diagram proposes almost exactly this shape for the *product*,
not yet the nav:

> Operating Library is a clearer umbrella label than "Workspace items." It could
> contain Agents, Workflows, Processes, Prompts, Resources, and templates, while
> Settings remains a separate configuration area and Projects remains the delivery
> area.

So the domain model already argues for three tiers: **configuration** (enables) →
**library** (reusable, definition-shaped) → **delivery/execution** (dated, in-progress).
That maps fairly directly onto a nav grouping question: does the sidebar reflect that
tiering, or does it stay a flat list of modules?

## Option A — Workspace / Library / Settings (three groups)

| Group | Contents | Rationale |
|---|---|---|
| **Workspace** | Dashboard, Projects (with Opened projects/sessions as its recency section, not a separate flyout), Collaboration | "What am I working on right now." Opened items become a subsection *of* Workspace instead of a bolted-on drawer — it's recency scoped to delivery context, which is what it already is in practice. |
| **Library** | Agents, Workflows, Processes, Prompt Gallery, Resources, Plugins, Scheduler, Test Lab | Reusable, definition-shaped things you configure once and launch/reference many times — this is the domain model's "Operating Library" almost verbatim. |
| **Settings** | Settings (all sections), provider/database profile switcher | One entry point. The DB switcher moves here as a named section (`/settings/database` or similar) rather than a separate bottom flyout that re-shows the same dialog. |

CRM/HR is the awkward one here — it's people/commercial data, not really "library" and
not really "current work." It could sit in Workspace (it feeds staffing into active
delivery) or stand alone. Simplest fix for a 3-group version: put it in Workspace, since
assignments and workforce data exist to serve project delivery.

**Trade-off:** three groups is the fewest that says anything distinct, but it forces
CRM/HR and Test Lab into groups they only partly belong to.

## Option B — Delivery / Library / People / Configuration (four groups)

Follows the domain model's subgraphs more literally:

| Group | Contents |
|---|---|
| **Delivery** | Dashboard, Projects (+ Opened), Collaboration |
| **Library** | Agents, Workflows, Processes, Prompt Gallery, Resources, Plugins, Scheduler |
| **People & Assurance** | CRM/HR, Test Lab |
| **Configuration** | Settings, database/provider switching |

**Trade-off:** cleaner conceptual fit, but "People & Assurance" is a group invented for
this document — it has no existing product identity, and grouping CRM/HR with Test Lab
only because both are "not quite library, not quite delivery" is a residual-bucket
smell, not a real relationship. If you go this route it needs a better name or you'll
just be trading one confusing catch-all (BottomUtilities) for another.

## Option C — Two groups + pinned rail (minimal change)

Keep almost everything as one list, but split only what's clearly bimodal:

| Group | Contents |
|---|---|
| **Workspace** (top, unlabeled or lightly labeled) | Dashboard, Projects (+ Opened), Collaboration, CRM/HR |
| **Library** (labeled section header, no group chrome) | Agents, Workflows, Processes, Prompt Gallery, Resources, Plugins, Scheduler, Test Lab |
| **Settings** | pinned single item, bottom of the nav, no separate utility bar entry |

This is closest to what exists today — same flat scrollable nav, just with two section
headers inserted and Settings/Opened/DB cleaned up — rather than a new visual system of
collapsible groups. Lowest implementation risk, and it directly answers your "could we
just make Opened a group under Workspace" question: yes, drop the flyout, make it a
collapsible subsection under the Workspace header.

**Gap:** this puts Agents, Workflows, Processes, and Scheduler in Library as if they
were only catalogs. But the domain model's critical distinction #1 is explicit that a
*definition* (reusable) and a *run/operation/conversation* (dated instance) are not the
same thing and must not collapse into one nav slot. Live and historical execution —
process runs (`/processes/runs`), workflow runs, agent sessions, and schedule-plan run
history — has no place of its own in this version; it would sit buried under a
"Processes" or "Agents" item in Library, one or two clicks deep, even though it's often
the thing someone needs fastest ("did the run finish," "what failed overnight").

## Option C-revised — add a peer Execution section

Same minimal-change spirit, but split Library's contents by the definition/instance line
instead of merging them:

| Group | Contents |
|---|---|
| **Workspace** | Dashboard, Projects (+ Opened), Collaboration, CRM/HR |
| **Execution** | Process runs (`/processes/runs`), workflow runs, agent sessions, schedule-plan run history — cross-definition, dated, "what's happening or happened" |
| **Library** | Agents, Workflows, Processes, Prompt Gallery, Resources, Plugins, Scheduler, Test Lab — reusable, definition-shaped, one entry per catalog |
| **Settings** | pinned single item, bottom of the nav |

Names here are pulled straight from `domain-model.md`'s vocabulary table (process run,
workflow run, agent session/operation, schedule plan) rather than the current UI's own
labels — "Live processes" is the tab name the canonical-route proposal already retires
in favor of `/processes/runs`, precisely so it stops reading as one more ad hoc status
view. Reusing "Live processes" in nav copy would reintroduce the naming drift this whole
exercise is trying to remove.

Each Library item can still deep-link to its own runs (`/agents/{id}/activity`,
`/processes/{id}/runs`) — Execution is the cross-cutting, definition-agnostic view, not
a replacement for those. This mirrors how Projects already isn't "buried" behind a
generic catalog: execution deserves the same peer status because it's equally
time-sensitive, just cross-cutting rather than project-scoped.

Alternative if a fourth header feels like too much chrome: fold Execution in as a
second subsection of Workspace, alongside Opened — "what's active" (Opened + Execution)
versus "what's reusable" (Library). That keeps it to two headers but still gives runs a
named, expandable home instead of leaving them nested inside catalog items.

### Detailed breakdown

Sublinks are the existing peer routes from `navigation-proposal.md`; entries marked
*(proposed)* have no canonical route yet and would need one before the nav item is more
than a label.

Sublinks are listed as `link` — Title, in the same order the route tables in
`navigation-proposal.md` use. A `{param}` route is a dynamic detail page, not a static
nav destination — it's included because it's still part of that item's route family and
determines what "opening" that item actually navigates to.

**Gap found while filling this in:** the current app's Workflows surface is bigger than
`navigation-proposal.md` documents. Today `/agents/workflows` is one page with five
in-memory tabs (no query-string binding, so none of this is a real URL today): Dashboard,
Workflows (catalog), Editor, History, Analytics. The proposal only names catalog/detail
/design/runs routes — Dashboard and Analytics aren't in it at all. Mapped into this
table: Dashboard becomes what `/workflows` itself is (a catalog root with an activity
summary, not a separate route); History is already Execution's Workflow runs item
(today's tab just merges run timeline, artifacts, and pending requests into one view);
Analytics has no home in either doc and is added below as its own Execution row, since
it's reporting built from run data, not a reusable definition.

<table>
<thead>
<tr><th>Group</th><th>Title</th><th>Link</th><th>Sublinks</th></tr>
</thead>
<tbody>
<tr><td rowspan="5">Workspace</td><td>Dashboard</td><td><code>/</code></td><td>—</td></tr>
<tr><td>Projects</td><td><code>/projects</code></td><td>
<code>/projects/{projectId}</code> — Details<br>
<code>/projects/{projectId}/structure</code> — Structure<br>
<code>/projects/{projectId}/gantt</code> — Gantt<br>
<code>/projects/{projectId}/files</code> — Files<br>
<code>/projects/{projectId}/management</code> — Management<br>
<code>/projects/{projectId}/processes</code> — Processes
</td></tr>
<tr><td>Opened</td><td>no route of its own — expandable subsection, not a page</td><td>
<code>/projects/{projectId}</code> — each recently opened project<br>
<code>/agents/{agentId}/chats</code> — each open agent chat session<br>
<code>/prompts/{promptId}</code> — each open prompt/workbench session <em>(exact session route TBD — current sessions aren't URL-addressable per [navigation-and-structure.md](navigation-and-structure.md))</em>
</td></tr>
<tr><td>Collaboration</td><td><code>/collaboration/inbox</code></td><td>
<code>/collaboration/inbox</code> — Inbox<br>
<code>/collaboration/threads</code> — All threads<br>
<code>/collaboration/threads/{threadId}</code> — Thread detail<br>
<code>/collaboration/escalations</code> — Escalations
</td></tr>
<tr><td>CRM/HR</td><td><code>/crm-hr</code></td><td>
<code>/crm-hr/directory</code> — Directory<br>
<code>/crm-hr/directory/{partyId}</code> — Party detail<br>
<code>/crm-hr/accounts</code> — Accounts<br>
<code>/crm-hr/accounts/{partyId}</code> — Account detail<br>
<code>/crm-hr/opportunities/{opportunityId}</code> — Opportunity detail<br>
<code>/crm-hr/workforce</code> — Workforce<br>
<code>/crm-hr/workforce/{partyId}</code> — Workforce profile<br>
<code>/crm-hr/recruiting</code> — Recruiting<br>
<code>/crm-hr/recruiting/{applicationId}</code> — Application detail<br>
<code>/crm-hr/agents</code> — Business-facing agents<br>
<code>/crm-hr/agents/{partyId}</code> — Business-facing agent detail<br>
<code>/crm-hr/assignments</code> — Assignments<br>
<code>/crm-hr/assignments/{assignmentId}</code> — Assignment detail<br>
<code>/crm-hr/staffing-requests/{requestId}</code> — Staffing request detail
</td></tr>
<tr><td rowspan="5">Execution</td><td>Process runs</td><td><code>/processes/runs</code></td><td>
<code>/processes/runs/{runId}</code> — Run detail
</td></tr>
<tr><td>Workflow runs</td><td><code>/workflows/runs</code> <em>(proposed aggregate)</em></td><td>
<code>/workflows/{workflowId}/runs</code> — Runs for one workflow (exists today)<br>
<code>/workflows/runs/{runId}</code> — Run detail<br>
<em>replaces today's monolithic "History" tab on `/agents/workflows` — timeline, artifacts, and pending human-in-loop requests stay part of the run detail</em>
</td></tr>
<tr><td>Workflow analytics</td><td><em>(proposed — today the "Analytics" tab on `/agents/workflows`, backed by `WorkflowAnalyticsQueryService`)</em></td><td>—</td></tr>
<tr><td>Agent sessions</td><td><em>(proposed cross-agent view)</em></td><td>
<code>/agents/{agentId}/chats</code> — Per-agent chat sessions (exists today)<br>
<code>/agents/chats</code> — General/simple chat workspace (exists today)
</td></tr>
<tr><td>Schedule history</td><td><code>/scheduler/history</code></td><td>
<code>/scheduler/schedules/{scheduleId}</code> — Schedule detail
</td></tr>
<tr><td rowspan="8">Library</td><td>Agents</td><td><code>/agents</code></td><td>
<code>/agents/{agentId}</code> — Overview<br>
<code>/agents/{agentId}/configuration</code> — Configuration<br>
<code>/agents/{agentId}/capabilities</code> — Capabilities<br>
<code>/agents/{agentId}/governance</code> — Governance<br>
<code>/agents/{agentId}/activity</code> — Activity<br>
<code>/agents/teams/{teamId}</code> — Team overview<br>
<code>/agents/providers</code> — Provider profiles<br>
<code>/agents/capabilities</code> — Capability catalog<br>
<code>/agents/diagnostics</code> — Diagnostics
</td></tr>
<tr><td>Workflows</td><td><code>/workflows</code> <em>(catalog root; absorbs today's "Dashboard" tab — `WorkflowOverviewPanel` — as the landing view)</em></td><td>
<code>/workflows/{workflowId}</code> — Overview<br>
<code>/workflows/{workflowId}/design</code> — Designer
</td></tr>
<tr><td>Processes</td><td><code>/processes</code></td><td>
<code>/processes/{processId}</code> — Overview<br>
<code>/processes/{processId}/design</code> — Design/editor<br>
<code>/processes/{processId}/roles</code> — Roles and assignments<br>
<code>/processes/{processId}/activity</code> — Activity
</td></tr>
<tr><td>Prompt Gallery</td><td><code>/prompts</code></td><td>
<code>/prompts/{promptId}</code> — Prompt detail
</td></tr>
<tr><td>Resources</td><td><code>/resources</code></td><td>
<code>/resources/browse</code> — Browse<br>
<code>/resources/{resourceId}</code> — Resource detail
</td></tr>
<tr><td>Plugins</td><td><code>/plugins</code></td><td>
<code>/plugins/{pluginId}</code> — Plugin detail
</td></tr>
<tr><td>Scheduler</td><td><code>/scheduler</code></td><td>
<code>/scheduler/schedules</code> — Schedules
</td></tr>
<tr><td>Test Lab</td><td><code>/test-lab</code></td><td>
<code>/test-lab/plans/{planId}</code> — Plan detail
</td></tr>
<tr><td>Settings</td><td>Settings</td><td><code>/settings</code></td><td>
<code>/settings/workspace</code> — Workspace<br>
<code>/settings/data-sources</code> — Data sources<br>
<code>/settings/storage</code> — Storage<br>
<code>/settings/files</code> — Files<br>
<code>/settings/secrets</code> — Secrets<br>
<code>/settings/database</code> — Database/profiles <em>(proposed — currently the standalone switch-database dialog)</em>
</td></tr>
</tbody>
</table>

Two items worth flagging from this table alone: **Workflow runs** and **Agent
sessions** don't have an aggregate cross-definition route today — they're proposed
here to fill the Execution group, but that's new IA, not a relabeling of something that
exists. Agents' Chats sublink moved out of Library into Execution's Agent sessions,
since a chat workspace is itself execution (a dated conversation), not a definition —
keeping it under the Agents catalog item would repeat the exact burying problem this
option set out to fix.

## Recommendation

Start with **Option C-revised**. It fixes all four original problems plus the
execution-visibility gap, without inventing groups that lack a home in the product
vocabulary:

1. Section headers (Workspace, Execution, Library — or Execution folded into Workspace)
   replace the flat list — this alone should shrink or eliminate the overflow problem,
   since a labeled section can collapse independently instead of everything competing
   for one capacity budget.
2. Opened projects/sessions become a subsection under Workspace (expandable, badge
   count preserved) instead of a separate flyout mechanism.
3. Live/historical runs get a named home instead of being nested one level inside
   whichever catalog item happens to own them.
4. Settings becomes one nav entry, pinned at the bottom of the list, full stop. Delete
   the `BottomUtilities` "Open workspace settings" button — it navigates to the exact
   same route today, so nothing is lost.
5. The database flyout either moves into Settings as a real section, or — if quick
   switching genuinely needs to stay one click away from anywhere — becomes a top-bar
   control (next to breadcrumbs) rather than a nav-adjacent popover, since it is an
   environment switcher, not a navigation destination.

CRM/HR staying in Workspace (rather than needing its own group) is the one modeling
call worth confirming with you before implementing — it's a judgment call, not something
derivable from the domain model doc.

## Open questions

- Does Collaboration belong in Workspace (current work) or is it closer to Library
  (it's about threads that outlive any one project)? The domain model's assurance
  subgraph groups it with Test Lab, but your instinct to put actionable items under
  Workspace also has a strong case if collaboration is mostly "things needing my
  response now." Note Collaboration is itself execution-shaped (durable, dated threads,
  not reusable definitions) — another point in favor of Workspace/Execution over Library.
- Should Test Lab move to Workspace instead of Library once assurance work is
  per-project rather than catalog-shaped in the UI?
- Is a persistent database/environment indicator in the top bar (always visible, not a
  popover) more useful than any nav placement, given how infrequently profiles are
  actually switched?
- For Execution as a peer section: does it need its own top-level route (an aggregate
  "what's running/ran across everything" view), or is it purely a nav grouping over
  existing per-definition run routes (`/processes/runs`, `/workflows/{id}/runs`, etc.)
  with no new page behind it? The nav grouping works either way, but an aggregate view
  is a larger, separate scoping decision worth calling out before committing to it.
