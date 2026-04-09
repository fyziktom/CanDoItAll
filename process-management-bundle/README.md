# CanDoItAll process-management execution-grade bundle

This bundle is the **process-first** delivery package for adding `CanDoItAll.Modules.Processes` directly into CanDoItAll before the intelligence lake.

Compared with the previous pass, this revision was rechecked against the uploaded **CanDoItAll** repo and the uploaded **CanDoItAll.AgentFramework** repo. It now hardens the package around:

- process as the **canonical collaboration and handoff graph**
- normalized **work briefs** and persisted **baton handoffs**
- governed **triage/routing** inside the process rather than hidden direct agent wiring
- live process supervision on the same canvas as a **projection**
- explicit canonical ownership for **CRM-HR**, **Workspace**, **Projects**, and future **AgentFramework** seams

## What is inside

- `00-context/` review notes, coverage mapping, and repo-fit analysis
- `01-workbooks/` execution-grade spreadsheets and rendered previews
- `02-architecture/` implementation architecture, operating model, and convergence guidance
- `03-subbundles/` feature-level Codex-ready implementation packs
- `04-codex/` execution order, prompts, and review checklist
- `05-manifest/` machine-readable manifests for features, stories, risks, traceability, and review output

## Main conclusions

1. `CanDoItAll.Modules.Processes` must remain the canonical owner of process definitions, orchestration, work briefs, baton handoffs, and journals.
2. CRM-HR remains the durable owner of human/AI identities and reusable role/agent templates.
3. Workspace remains the durable owner of provider/model profile truth.
4. Projects remain the owner of project scope and context; processes link to project objects through typed references instead of copying hierarchy.
5. Live execution on canvas is valuable, but it must stay a **projection**, never the source of truth.
6. Future AgentFramework integration should happen only through a later bridge/adapter that correlates runtime evidence back to `ProcessRun` and `ProcessStepRun`.

## Current bundle counts

- Features: 24
- User stories: 102
- Risks: 28
- Decisions: 26
- Entities: 44
- Integrations: 15
- Repo touchpoints: 104
- Subbundles: 24

## Recommended first read

1. `00-context/coverage-checklist.md`
2. `00-context/10-cross-repo-convergence-review.md`
3. `00-context/11-senior-csharp-architect-review.md`
4. `00-context/09-final-readiness-review.md`
5. `01-workbooks/01-process-management-execution-grade.xlsx`
6. `01-workbooks/02-process-modeling-canvas-and-runtime.xlsx`
7. `02-architecture/12-process-native-orchestration-and-baton-handoffs.md`
8. `02-architecture/13-cross-repo-convergence-processes-projects-and-agentframework.md`
9. `04-codex/IMPLEMENTATION_ORDER.md`
