# 3. Context map and ownership

## Logical communication map

```text
UI / HTTP / agent tools / workflow executors / process steps
                      |
          typed, purpose-scoped integration adapters
                      |
      canonical application API of the destination owner
                      |
       owner invariants, persistence, receipts, and events
```

Agents, Workflows, and Processes can read and request changes from CRM, Simple Chats, Resources, Scheduler, Prompts, Projects, Structure, TestLab, and other explicitly supported owners. **Structure is not an intermediate gateway for unrelated CRM or Simple Chats administration.** Logical bidirectional communication must not become a circular project-reference graph. See [contract rules](04-contract-rules.md) and [automation adapters](14-cross-module-operations-and-runtime-adapters.md).

## Authority by fact

| Fact | Single target owner | Permitted projection or independent enrichment |
|---|---|---|
| Technical agent ID/name/status/configuration/capability assignments | Agents | CRM/Structure summaries; local CRM personnel note or Structure alias stays local. |
| Provider/model/tariff/publication | Providers | Versioned quotes/cache; no independent CRM AI-price editor. |
| Observed invocation usage and dispatch tariff | Agents/Providers evidence | Process/task allocations reference evidence without duplicate billing. |
| Party/contact/affiliation/confidential information | CRM | Purpose-filtered summaries, never a complete Party in every picker. |
| CRM staffing/resource profile and technical-agent binding | CRM | Planning projections; not a second technical agent. |
| Commercial/staffing project participation | CRM | Projects delegates role operations; Projects validates project lifecycle. |
| Project, phases, portfolio hierarchy | Projects | Structure projects relationships and delegates their mutation. |
| Native simple-note body | Structure | Snapshot, export, and authorized Memory source; not lazy file contents. |
| Native placement, annotation, structural link | Structure | Views/context; consumers do not write its rows. |
| Work item, schedule, dependency, assignment | Work Management within Structure | CRM workload, Gantt, and agent read models. |
| Capacity block/reservation | CRM/Staffing | Work plan may request reservation; execution admission is separate. |
| Process definition/run/step state | Processes | Structure status projection with source version and lineage. |
| Workflow definition/version/run/checkpoint | Workflows | Structure references and delivery state, not another run store. |
| Run artifact manifest | Respective execution owner | Destination references; Storage owns bytes. |
| Storage object/version/locator/physical cleanup | Storage | Attachment owner publishes reference eligibility. |
| Catalog Resource metadata | Resources | Structure projection; promotion from Storage is explicit. |
| Simple Chat definition/revision and ordinary conversation state | Simple Chats | External administration receives only separately authorized definition fields. |
| Prompt content/version/compatibility | Prompts | Pinned use by a run; local alias is distinct. |
| Test plan/run/verdict/evidence metadata | TestLab | Target references; evidence bytes are in Storage. |
| Schedule plan/firing/dispatch history | Scheduler | Trigger engine is an operational projection; target owns its run. |
| Derived Memory index/retrieval | Memory | No authority over source facts. |
| Plugin installation/grants/connection | Plugins | Runtime availability projection; secret values remain Security. |
| Secret value/protection | Security | Opaque reference or purpose-limited handle only. |
| Workspace preference | Workspace | Provider references remain foreign identities. |
| Database profile activation/incarnation/host capability | Control plane | Domains receive resolved operation scope, not global mutable current state. |
| Window/editor/browser state | Shell/UI session | No database master authority; invalidate on scope change. |

## One card can show several owners

A CRM agent card may show technical name from Agents, affiliation from CRM, workload from Work Management, and quote from Providers. Each field retains provenance/revision. A coordinated form must not disguise multiple commits as one local entity save. Preserve any existing atomic UX guarantee with real transaction coordination; a new multi-step partial outcome requires explicit approval, not an incidental regression.

## Data overlaps requiring classification

**ProjectPartyAssignment:** classify each role as project participation, work-item assignment, or runtime execution assignment [MAP-01, MAP-08]. Preserve metadata, intervals, rates, primary flags, audit, and supported multiplicity. Ambiguous rows need a reconciliation report.

**WorkItem/native task node:** they may initially share one canonical persistence record. Do not add a second Tasks table and bidirectional synchronization. Work Management controls task semantics; graph operations control structural fields. Do not split atomic changes within the same aggregate artificially.

**Workflow/process node:** Structure owns placement, definition reference, input binding, and local annotation. Runtime owns definition/run. Current status is a labeled projection; LastRunId is a convenience pointer, not complete history.

**Secret-reference node:** deleting the reference does not delete its secret. Separate owner permission and usage checks apply.

**Resources versus staffing resources:** a CRM resource is a planning/personnel concept; Resources is a catalog of materials/information; Storage contains bytes. Similar names do not establish shared CRUD ownership.

## New kinds

Before adding a kind, establish identity, owner, lifecycle, queries/commands, relations, scope/security, storage, and retention. Use a versioned descriptor/adapter for external kinds, not reflection-based foreign EF imports. Unknown kinds may render as safe read-only placeholders; they are not arbitrary mutable objects. Model input cannot register privileged kinds or handlers.
