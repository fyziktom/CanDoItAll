# 07 — Implementation Plan

## 1. Planning approach

The implementation plan is designed for:
- one lead developer or architect
- Codex-assisted execution
- incremental vertical slices
- constant validation
- limited tolerance for large speculative rewrites

The plan deliberately implements **product value first**, **risk controls early**, and **expensive integrations behind stable abstractions**.

## 2. Delivery strategy

### 2.1 Sequence principles
1. Build the shell and module seams first.
2. Lock persistence and secret handling early.
3. Deliver project and resource management before advanced prompt automation.
4. Deliver prompt library before full prompt factory automation.
5. Deliver validation before deep automation or execution features.
6. Build tests in parallel, not at the end.
7. Keep every phase shippable.

### 2.2 Milestone principles
Each milestone must end with:
- demonstrable user-visible value
- committed automated tests
- documented open risks
- clear handoff to the next milestone

## 3. Milestone overview

| Milestone | Theme | Outcome |
|---|---|---|
| M0 | Foundation | Solution bootstrapped with module seams, shell, and conventions |
| M1 | Persistence and security | DB, `DbContextFactory`, storage abstraction, secret vault baseline |
| M2 | Workspace and providers | Provider profiles, workspace defaults, health checks |
| M3 | Projects and stack profile | Project creation, dates, phases, generalized options |
| M4 | Resources and connectors | Typed resources, secret references, validation states |
| M4A | Workbench and visual orchestration | Internal tabs, tab restore, project structure canvas, project calendar |
| M5 | Prompt library | Drafts, versions, collections, tags, usage history |
| M6 | Prompt factory | Phase wizard, blueprints, context assembly, prompt validation |
| M7 | Validation center | Review flows, checklists, findings, coverage links |
| M8 | Test lab and evidence | Test planning, screenshot records, Playwright linkage |
| M9 | Hardening | audit, search, background jobs, packaging, docs cleanup |

## 4. Detailed milestone plan

## M0 — Foundation

### Goals
- create solution structure
- create Blazor host
- create module projects
- create shared kernel and infrastructure baselines
- integrate Tailwind build flow
- create shell and navigation skeleton
- set coding and testing conventions

### Tasks
1. Create solution and projects.
2. Create module registration pattern.
3. Create shared result/error primitives.
4. Create shell layout, internal tab host contracts, and page placeholders.
5. Integrate the existing component set and create ComponentKit wrappers.
6. Add test projects.
7. Add lint/format/test scripts.
8. Create architecture README inside repo.

### Deliverables
- compilable solution
- working shell
- internal tab shell baseline
- placeholder routes for all main areas
- CI-friendly test command baseline
- initial component styling foundation

### Acceptance criteria
- solution builds cleanly
- shell navigation works
- the shell has a credible internal tab workspace baseline
- all modules register through a predictable pattern
- Tailwind pipeline works
- test projects run

## M1 — Persistence and security baseline

### Goals
- implement persistence foundation
- implement database provider selection
- add runtime `IDbContextFactory`
- add design-time factory
- add file storage abstraction
- add secret protection abstraction
- add audit and logging redaction foundations

### Tasks
1. Create `AppDbContext`.
2. Add module-owned EF configurations.
3. Implement database provider bootstrap for SQLite and PostgreSQL.
4. Implement `IDesignTimeDbContextFactory<AppDbContext>`.
5. Implement workspace root path resolver.
6. Implement managed file store abstraction.
7. Implement `ISecretProtector`.
8. Create secret metadata and encrypted payload persistence.
9. Add safe logging helpers.

### Deliverables
- database bootstraps
- migrations baseline
- secure secret storage service
- storage root management
- logging redaction helpers

### Acceptance criteria
- app runs with SQLite
- app runs with PostgreSQL configuration
- migrations can be created
- secret round-trip works
- no plain-text secret value is logged

## M2 — Workspace and providers

### Goals
- implement settings UI
- implement provider profile CRUD
- implement health checks
- implement workspace defaults
- prepare provider abstraction for OpenAI and Ollama

### Tasks
1. Build settings pages.
2. Implement provider profile entity and CRUD flows.
3. Implement safe secret reference picker for API keys.
4. Implement provider profile validation.
5. Implement capability flags.
6. Implement provider abstraction interfaces.
7. Add health display widgets.

### Deliverables
- workspace settings UI
- provider profile management
- provider health checks
- stored provider defaults

### Acceptance criteria
- OpenAI profile can be configured
- Ollama local profile can be configured
- Ollama remote profile can be configured
- provider health can be tested
- invalid settings surface actionable errors

## M3 — Projects and stack profile

### Goals
- implement project creation/editing
- implement dates, phases, and statuses
- implement generalized option selections
- implement stack profile UI

### Tasks
1. Implement project aggregate and tables.
2. Implement phase editor and status handling.
3. Implement option catalog and selection model.
4. Build project creation wizard.
5. Build project overview page.
6. Build stack profile page.
7. Implement recent/recommended project summaries.

### Deliverables
- project creation workflow
- project overview page
- stack profile editor
- option selection and notes persistence

### Acceptance criteria
- new project can be created end-to-end
- phases and dates persist correctly
- option notes persist correctly
- project summaries load quickly
- project editing is covered by tests

## M4 — Resources and connectors

### Goals
- implement generalized resource model
- implement descriptor registry
- implement typed resource editors
- implement secret references in resources
- implement validation status and preview/indexing flags

### Tasks
1. Create `ProjectResource`.
2. Create resource descriptor contracts.
3. Build add-resource flow.
4. Implement editors for required resource kinds.
5. Add validation state tracking.
6. Add preview/indexing capability metadata.
7. Add connector profile reuse.
8. Add resource detail drawer.
9. Add resource list filters and badges.

### Deliverables
- resource management UI
- typed editors for required resource kinds
- validation and sensitivity indicators
- reusable resource registry

### Acceptance criteria
- every required resource kind can be registered
- secret references work for FTP/SSH/provider scenarios
- resource records display status clearly
- resource add/edit flows are test-covered
- no raw secret duplication occurs in resource rows

## M4A — Workbench and visual orchestration

### Goals
- implement the internal tab workspace
- implement browser-state-backed restore and sleeping-tab behavior
- implement the project structure canvas wrapper
- implement the project events calendar wrapper
- link workbench surfaces to real project artifacts

### Tasks
1. Create the Workbench module and persistence model.
2. Implement tab host services, tab registry, and browser-storage-backed restore.
3. Build shell tab strip, pinning, reordering, close actions, and restore UX.
4. Implement the project structure canvas wrapper using the documented JavaScript engine.
5. Implement the project events calendar wrapper using the documented JavaScript widget.
6. Build inspector or outline surfaces around the structure canvas.
7. Support opening related artifacts from the canvas and calendar into internal tabs.
8. Add tests for tab restore, sleep or wake, and the wrapper contracts.

### Deliverables
- Workbench module
- internal tab workspace
- local restore path
- project structure canvas wrapper
- project calendar wrapper
- automated tests

### Acceptance criteria
- users can open, close, reorder, pin, and restore internal tabs
- refresh or reconnect restores the prior tab session safely
- heavy tabs can sleep without losing recoverable state
- the structure canvas is usable through a wrapper-first integration
- the project calendar is usable through a wrapper-first integration
- linked artifacts open into internal tabs

## M5 — Prompt library

### Goals
- implement prompt draft/final/version model
- implement prompt collections and tags
- implement prompt search
- implement prompt usage history

### Tasks
1. Create prompt domain entities.
2. Build prompt CRUD pages.
3. Build prompt detail and version history.
4. Build collections/galleries.
5. Add tag model and filter UI.
6. Add usage record model with repo/commit metadata.
7. Add clone flow.

### Deliverables
- prompt gallery
- prompt editor
- version history
- collections
- usage timeline

### Acceptance criteria
- prompt draft can be saved and edited
- final version can be created without overwriting history
- tags and filters work
- usage can be linked to project and repository metadata
- prompt clone flow works

## M6 — Prompt factory

### Goals
- implement guided prompt wizard
- implement blueprint catalog
- implement context assembly
- implement pre-send validation
- implement save/export/send flows

### Tasks
1. Implement `PromptBlueprint`.
2. Build wizard stepper.
3. Implement context assembly pipeline.
4. Implement generated prompt preview.
5. Implement prompt validation warnings.
6. Implement save-as-draft/save-as-final.
7. Implement copy/export.
8. Implement provider send path through the provider abstraction.

### Deliverables
- prompt factory UI
- blueprint selection
- prompt generation pipeline
- provider send/export paths
- build session persistence

### Acceptance criteria
- user can create a prompt from a project phase in one guided flow
- selected resources and options appear in assembled context
- validation warnings surface before send
- generated prompt can be saved, copied, exported, or sent
- build session is traceable

## M7 — Validation center

### Goals
- implement validation models
- implement checklist engine
- implement findings and action tracking
- implement architecture/plan/layout/story review flows

### Tasks
1. Create validation domain.
2. Implement checklist persistence.
3. Build validation center UI.
4. Implement deterministic validation rules for initial review types.
5. Build findings panel and decision actions.
6. Add linkage between validation and project artifacts.

### Deliverables
- validation center
- validation run records
- findings and actions
- baseline review workflows

### Acceptance criteria
- user can run and store a validation
- findings are persisted with severity and action
- validation runs can be reopened later
- review flows map to project artifacts
- at least stories/layout/architecture/plan validations are present

## M8 — Test lab and evidence

### Goals
- implement test planning model
- implement test case linkage
- implement screenshot evidence records
- implement Playwright linkage
- implement quality dashboard summary

### Tasks
1. Create test lab domain.
2. Build test plan page.
3. Build evidence upload/reference flows.
4. Add linkage to stories/phases/features.
5. Add latest run/result cards.
6. Add quality dashboard widgets.

### Deliverables
- test lab UI
- screenshot/evidence records
- test plans and linked cases
- phase/story coverage traceability

### Acceptance criteria
- planned tests can be recorded
- implemented tests can be linked
- executed results can be recorded
- screenshot evidence is traceable
- Playwright-related records fit the test lab model

## M9 — Hardening, search, activity, packaging

### Goals
- implement activity timeline
- implement search documents
- implement background job visibility
- harden audit and redaction
- improve packaging and startup experience
- finalize documentation inside the repo

### Tasks
1. Implement activity entries and timeline.
2. Implement search document indexing and query service.
3. Add background job UI and status records.
4. Add stronger diagnostics and health surfaces.
5. Add export/import foundations if feasible.
6. Final cleanup and release checklist pass.

### Deliverables
- activity timeline
- searchable records
- background task visibility
- hardened logging/audit
- shippable internal beta

### Acceptance criteria
- major actions create activity entries
- prompt and project search returns useful results
- background jobs are visible to the user
- safety checks pass
- internal release checklist passes

## 5. Parallel workstreams

Some work should run in parallel to reduce late surprises.

### Workstream A — Testing infrastructure
Runs from M0 onward:
- test project setup
- test data builders
- base mocks/fakes
- Playwright harness
- component testing harness

### Workstream B — UI component library hardening
Runs from M0 onward:
- shell components
- reusable form groups
- list/detail templates
- wizard components
- badges/status patterns
- internal tab-strip and workbench-shell components

### Workstream C — Visual workbench wrappers
Runs from M0 onward:
- project structure canvas wrapper preparation
- project calendar wrapper preparation
- typed DTO contracts for both wrappers
- browser-storage-backed restore contracts

### Workstream D — Documentation and ADR tracking
Runs continuously:
- update internal docs
- record deviations from the plan
- update coverage notes
- maintain implementation constraints

## 6. Suggested implementation order inside the codebase

### Order of dependency
1. SharedKernel
2. Infrastructure
3. Security
4. Workspace
5. Projects
6. Resources
7. Workbench
8. Prompts
9. Factory
10. Validation
11. TestLab
12. Activity
13. Automation
14. Web shell refinements

## 7. Definition of done

A milestone is done only when:
- functional scope is implemented
- tests exist at the intended level
- no blocking TODO placeholders remain
- documentation comments are in English
- critical logs are redacted
- UI empty/error/loading states exist
- acceptance criteria are demonstrably satisfied

## 8. Phase gates

## Gate G0 — after M0
Proceed only if:
- solution builds
- module registration pattern is stable
- shell is working
- Tailwind and test harnesses are in place

## Gate G1 — after M1
Proceed only if:
- SQLite and PostgreSQL bootstraps are working
- secret protection works
- migrations work
- safe logging baseline is verified

## Gate G2 — after M3
Proceed only if:
- project creation is stable
- option model is not collapsing into hardcoded one-offs

## Gate G3 — after M4
Proceed only if:
- all required resource kinds can be registered
- security references are functioning correctly

## Gate G3A — after M4A
Proceed only if:
- internal tabs can be restored safely after interruption
- the structure canvas and calendar wrappers are operational
- linked artifacts open inside internal tabs instead of forcing browser-tab workflows

## Gate G4 — after M6
Proceed only if:
- prompt factory is usable end-to-end
- save/export/send flows are stable
- no sensitive leakage has been observed in the send path
- prompt sessions can be re-entered through the workbench model

## Gate G5 — after M8
Proceed only if:
- validation center and test lab link coherently to projects and prompts

## 9. Not-to-do list during v1

Avoid these traps:
- building remote collaboration too early
- over-optimizing search
- building full autonomous agent execution
- implementing advanced semantic parsers for every file type before core workflows work
- introducing distributed infrastructure prematurely
- letting UI patterns diverge by module

## 10. Recommended release slices

### Slice 1 — Foundational alpha
Includes M0–M3  
User value:
- configure workspace
- create project
- define stack profile

### Slice 2 — Resource and prompt alpha
Includes M4–M5  
User value:
- attach resources
- manage prompt library

### Slice 2A — Workbench alpha
Includes M4A  
User value:
- keep project work inside internal tabs
- visualize project structure
- manage project schedule visually

### Slice 3 — Prompt factory beta
Includes M6  
User value:
- generate phase-aware prompts end-to-end

### Slice 4 — Validation beta
Includes M7–M8  
User value:
- review, test plan, evidence, quality traceability

### Slice 5 — Internal release candidate
Includes M9  
User value:
- activity, search, jobs, hardening

## 11. Managerial risk summary

### Highest schedule risk
Resources/connectors + prompt factory

### Highest quality risk
Secret handling and output/send/export safety

### Highest design risk
UI fragmentation from feature growth and a weak workbench model

### Highest technical risk
Background processing and provider differences

## 12. Final implementation guidance

The implementation team should stay disciplined:
- ship the shell early
- lock persistence and security early
- do not skip typed resource descriptors
- do not blur prompts and factory concerns
- do not push validation to the end
- build tests continuously
- keep every milestone user-demoable
