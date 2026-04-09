# Target module topology

## New canonical module

Create a new module:

- `src/CanDoItAll.Modules.Processes`

Suggested internal slices:

- `Domain/` definitions, versions, nodes, transitions, governance profile
- `Runtime/` runs, step runs, journal, assignments, work briefs, triage, exception handling
- `Governance/` ownership, interfaces, change requests, communications, decision rights
- `Telemetry/` metrics snapshots, bottleneck analysis, conformance review, runtime overlay projections
- `CanvasAdapters/` process designer adapters on top of CanvasLib
- `Pages/` catalog, designer, runtime, governance, metrics, conformance

## Future adapter seam

Do **not** merge the current external AgentFramework repo directly into the canonical process module.  
If and when the runtime bridge is introduced, prefer a later seam such as:

- `src/CanDoItAll.Modules.Processes.AgentFrameworkAdapter` (future)

That future adapter can depend on the merged AgentFramework runtime pieces while the canonical process model remains stable.

## Canonical ownership split

- `Processes` owns process definitions, versions, interfaces, runtime state, work briefs, governed routing, telemetry, and conformance records.
- `CRM-HR` owns durable human and AI identities, workforce capacity, recruiting, staffing requests, and reusable role or agent templates.
- `Workspace` owns shared provider profiles and neutral provider execution contracts.
- `Projects` owns project identity, hierarchy, and project-scoped navigation.
- `Workbench` remains projection-only for process references or summaries.
- `AgentFramework adapter` (future) may execute work, but does not become the business owner of templates, identities, providers, or process topology.

## Sensitive review seam

Conformance observations and restricted governance notes should be protected through Security-aware policies rather than embedded as freely readable comments.

## Typed context-link seam

The module should include typed references that link process definitions, runs, and steps to project objects or other business context.  
That avoids both extremes:

- duplicating the entire project hierarchy inside Processes
- or making process orchestration disappear into project-node metadata
