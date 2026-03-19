# 01 — UX Discovery, Roles, User Stories, and Use Cases

## 1. Product framing

### Product mission
PromptStudio is a local-first workbench that helps technical teams create, review, refine, store, and reuse prompts tied to real software delivery work. The product is not only a prompt editor; it is a **project-context prompt system** that understands phases, linked assets, stack choices, constraints, and validation criteria.

### Product promise
A user should be able to open one project workspace, see its current delivery phase, attach relevant technical assets, choose the target LLM/provider, and generate a phase-appropriate prompt with enough context to be immediately useful for Codex or another implementation model.

### UX philosophy
The UX must feel:
- structured, not chaotic
- powerful, not overwhelming
- modular, not fragmented
- consistent, even as more tools are added later
- transparent about what is generated, stored, encrypted, validated, or sent to external services

### Primary value propositions
1. Replace ad hoc prompt text files with structured prompt workflows.
2. Tie prompts to projects, phases, assets, and implementation history.
3. Reduce prompt quality variance through guided blueprints and checklists.
4. Make agent-assisted development auditable and repeatable.
5. Provide one cohesive workstation for architecture, implementation planning, review, validation, and testing prompts.

## 2. Product assumptions

### Confirmed assumptions
- The primary runtime is a local workstation.
- The main UI runs as a Blazor Web App with Interactive Server rendering.
- The app is primarily single-user in the first release, but the design must remain future-ready for multi-user expansion.
- The user works across many software projects with different stacks.
- The user wants strong control, not fully autonomous execution.

### Deliberate assumptions introduced by the architecture
- Roles exist logically even if v1 is effectively single-user.
- Secrets are managed centrally and linked into projects via references.
- All linked objects are treated as typed resources with a shared UX shell and resource-specific editors.
- Prompt generation must be phase-aware and validation-aware.
- Future microservices are optional and must not complicate v1 unnecessarily.

## 3. Actors and logical roles

## 3.1 Core roles

### Role R1 — Workspace Owner
The person who owns the local workspace, configures providers, sets defaults, controls security posture, and approves sensitive actions.

**Needs**
- full control over settings
- secure secret handling
- ability to approve execution-capable actions
- confidence that data is not leaking to providers unintentionally

### Role R2 — Solution Architect
The person designing systems, reviewing technical structure, and preparing implementation guidance.

**Needs**
- architecture prompts
- review prompts
- traceability between requirements, architecture, and implementation
- phase-driven workflows

### Role R3 — Developer / Implementer
The person using prompts to scaffold, refine, or validate implementation work.

**Needs**
- stack-aware prompt generation
- access to linked assets and repositories
- implementation checklists
- clear acceptance criteria

### Role R4 — QA / Reviewer
The person validating plans, UX, implementations, and tests.

**Needs**
- validation checklists
- review prompts
- defect and gap tracking
- screenshot/test evidence flows

### Role R5 — DevOps / Integrations Operator
The person handling environments, Docker, repositories, SSH, FTP, scripts, and deployment-linked assets.

**Needs**
- safe handling of connector profiles
- visibility into execution boundaries
- reusable connection profiles
- auditability of environment-related prompts

### Role R6 — Stakeholder / Observer
A read-mostly role for reviewing project structure, prompt outputs, decisions, and progress.

**Needs**
- clear summaries
- read-only views
- simple navigation
- low-complexity presentation of decisions

## 3.2 Future roles
- Team Administrator
- Security Officer
- Project Contributor
- External Reviewer

These future roles must influence policy design now, even if the first release runs as a trusted local workspace.

## 4. Personas

### Persona P1 — Lead Architect with multiple active projects
- Maintains 5–20 projects in parallel
- Switches often between architecture, validation, and implementation planning
- Needs a prompt system that preserves context without retyping it

### Persona P2 — Solo full-stack engineer
- Wants one tool to capture project metadata, resources, and prompts
- Prefers quick flows and reusable templates
- Needs practical outputs over academic rigor

### Persona P3 — Delivery lead using LLMs responsibly
- Wants AI help but requires oversight
- Needs history, review points, and repeatable quality checks
- Rejects “magic” UX that hides what happened

### Persona P4 — Technical QA with architecture awareness
- Wants to validate whether prompts and generated outputs align with requirements
- Needs strong links between stories, layouts, architecture, tests, and results

## 5. Jobs to be done

1. **When I start a new project**, help me define the project profile, stack, phases, and linked resources once so I can reuse them across the whole lifecycle.
2. **When I am at a specific delivery phase**, help me generate a prompt appropriate to that phase without manually rebuilding the whole context.
3. **When I revise an architecture or implementation plan**, help me compare it against requirements, stories, and earlier outputs.
4. **When I need project-linked prompts**, help me store, tag, search, version, and reuse them.
5. **When I work with repositories and assets**, help me include the right technical context safely.
6. **When I involve sensitive profiles or credentials**, protect them by default.
7. **When the application grows**, keep the UI coherent and the architecture modular.

## 6. UX capability map

### Capability group U1 — Workspace setup
- configure application defaults
- select storage mode
- manage provider profiles
- manage secret store
- define default component/theme behavior
- set default prompt blueprints

### Capability group U2 — Project management
- create and edit project
- define dates and phases
- define statuses
- select primary/secondary language
- select data/UI/storage/API options
- add project notes
- link assets and prompts

### Capability group U2A — Project orchestration workbench
- open project work inside internal application tabs
- restore a project work session after refresh, close, or crash
- keep inactive tabs sleeping without losing recoverable state
- visualize project structure as a canvas of phases, prompts, resources, validations, and tests
- schedule project events, reviews, milestones, and prompt deadlines in a project calendar
- open related artifacts from the structure canvas and calendar into internal tabs

### Capability group U3 — Resource management
- add typed resources
- validate access
- preview supported file types
- attach notes and tags
- control storage policy
- mark sensitive resources
- view last validation/indexing state

### Capability group U4 — Prompt library
- create prompt
- create prompt gallery/collection
- save template / blueprint / draft / final prompt
- tag and search prompts
- view usage history by project / phase / repo / time

### Capability group U5 — Prompt factory
- guided phase selection
- template selection
- auto-assembled context
- editable prompt preview
- validation before send/export
- save as draft/template/final
- send to provider or copy/export

### Capability group U5A — Prompt flow orchestration
- show prompt sessions and prompt steps inside the project structure canvas
- branch from any prompt step into a follow-up prompt
- trace which resources and prior prompts influenced each step
- reopen prompt work from the exact prior node without rebuilding context manually

### Capability group U6 — Review and validation
- validate stories/use cases
- validate layouts
- validate architecture
- validate implementation plan
- validate prototype against plan
- plan test coverage
- record findings and decisions

### Capability group U7 — Testing and evidence
- manage validation plans
- record screenshot evidence
- link Playwright scenarios
- store test outcomes
- connect evidence back to project phase

### Capability group U8 — Operations and extensibility
- launch or connect sidecar services later
- manage connector health
- import/export package data
- handle module growth without breaking navigation

### Capability group U8A — Development acceleration and assisted tuning
- observe normalized local watch, build, and runtime state
- wait for a trustworthy ready signal before browser verification
- keep compressed source capsules current from in-file comments
- inspect capsule coverage and drift
- enable a dev-only tuning mode with per-component targeting
- submit targeted tuning requests with screenshot and context
- review change readiness only after Codex and watch propagation complete

## 7. Project lifecycle map

## 7.1 Main lifecycle
1. Project setup
2. UX discovery
3. Architecture drafting
4. Architecture review
5. Implementation planning
6. Initial implementation
7. Prototype validation
8. Architecture revision
9. Test planning
10. Test implementation
11. Validation and iteration
12. Feature addition loop

## 7.2 Reusable feature loop
For every new feature:
1. feature context selection
2. architecture prompt
3. plan prompt
4. plan validation
5. implementation prompt
6. implementation validation
7. test plan prompt
8. test execution and evidence
9. closure note

The UX must make both the **main lifecycle** and the **feature loop** obvious and navigable.

## 8. Information inputs the UX must collect

### 8.1 Workspace-level inputs
- workspace name
- default storage root
- default database provider
- default UI theme preferences
- provider profiles
- secret handling policy
- default prompt output style
- default review strictness
- default artifact retention

### 8.2 Project-level inputs
- project name
- project description
- project status
- start date
- phase dates
- expected finish date
- primary language
- secondary languages
- data layer choice
- UI framework choice
- external API selections
- storage strategy
- architecture style note
- special constraints
- code generation notes
- testing notes

### 8.3 Resource-level inputs
- resource type
- display name
- technical location/path/URL
- optional credentials/profile reference
- tags
- notes
- project phase relevance
- security classification
- storage policy
- preview eligibility
- indexing eligibility

### 8.4 Prompt-level inputs
- prompt title
- prompt type (template, draft, final, review, blueprint)
- project phase
- target LLM/provider
- model preference
- applicable tech stack
- selected resources
- expected output format
- validation checklist
- notes
- version tags

## 9. Generalized option model

The UX must not hardcode every future technical choice as a custom screen. It needs a generalized option model.

### 9.1 Option groups
- primary language
- secondary language
- database type
- UI framework
- rendering mode
- styling approach
- repository strategy
- architecture style
- storage strategy
- testing strategy
- deployment style
- external APIs
- CI/CD strategy
- documentation style
- additional tools

### 9.2 Option record shape
Each selectable option must support:
- option code
- display name
- description
- category
- compatibility tags
- optional icon
- optional recommended flag
- optional deprecation flag
- user note
- selection order
- source (built-in or user-defined)

This design avoids continuous schema rewrites every time a new stack choice is introduced.

## 10. Epics

### Epic E1 — Manage Workspace Configuration
As a workspace owner, I want to configure global defaults and provider connections so every new project starts from a known baseline.

### Epic E2 — Manage Projects and Delivery Phases
As an architect or developer, I want to create and maintain project metadata, phases, and statuses so prompts can be generated against real delivery context.

### Epic E2A — Operate the Project Workbench
As an architect or delivery lead, I want one internal workbench with tabs, a structure canvas, and a project calendar so I can coordinate complex project work without relying on many browser tabs.

### Epic E3 — Link Technical Resources
As a user, I want to attach many resource types to a project so prompts can be grounded in actual source material.

### Epic E4 — Manage Prompts and Galleries
As a user, I want to create, version, group, search, and reuse prompts so I stop duplicating prompt work.

### Epic E5 — Generate Phase-Aware Prompts
As a user, I want a guided prompt factory so I can create context-rich prompts for architecture, implementation, testing, and reviews.

### Epic E6 — Validate Work Products
As a reviewer, I want structured validation flows so I can check alignment between stories, layouts, architecture, plans, code, and tests.

### Epic E7 — Preserve Traceability
As a lead, I want usage history, timestamps, repository references, and decisions so prompt-driven work remains explainable.

### Epic E8 — Operate Safely
As a workspace owner, I want credentials and dangerous actions to be carefully controlled so convenience does not undermine security.

### Epic E9 — Keep the UI Cohesive
As a user, I want a unified shell and consistent interaction patterns even as more tools are added.

### Epic E10 — Stay Extensible
As an architect, I want modular growth paths and future service boundaries so the app does not collapse under feature growth.

### Epic E10A — Accelerate Development Safely
As a developer or delivery lead, I want a machine-readable local change loop and targeted tuning workflow so the application can evolve quickly without relying on guesswork.

## 11. User stories

## 11.1 Workspace and setup stories
- **US-001** — As a workspace owner, I can define the default database provider for new workspaces.
- **US-002** — As a workspace owner, I can configure OpenAI and Ollama provider profiles independently.
- **US-003** — As a workspace owner, I can store API keys and passwords securely and reference them without exposing raw values.
- **US-004** — As a workspace owner, I can define default prompt blueprints for common project phases.
- **US-005** — As a workspace owner, I can test provider connectivity before saving a profile.

## 11.2 Project stories
- **US-006** — As a user, I can create a project with name, description, dates, phases, and status.
- **US-007** — As a user, I can define phase-specific dates and expected completion targets.
- **US-008** — As a user, I can select a primary language and optional secondary languages.
- **US-009** — As a user, I can choose project options such as DB type, UI stack, storage model, and external APIs.
- **US-010** — As a user, I can attach notes to each option so unusual decisions are preserved.
- **US-011** — As a user, I can reopen an existing project and immediately see its current phase and next relevant actions.

## 11.3 Resource stories
- **US-012** — As a user, I can attach a local folder to a project.
- **US-013** — As a user, I can attach one or more files of many types to a project.
- **US-014** — As a user, I can attach a web link with a title and note.
- **US-015** — As a user, I can attach an FTP profile and reference stored credentials.
- **US-016** — As a user, I can attach an SSH profile and reference stored credentials or key pairs.
- **US-017** — As a user, I can attach PowerShell scripts with explicit sensitivity labeling.
- **US-018** — As a user, I can attach a local or remote repository.
- **US-019** — As a user, I can attach Docker or Docker Compose resources.
- **US-020** — As a user, I can attach secret records to a project by reference rather than embedding them everywhere.
- **US-021** — As a user, I can validate whether a connector profile still works.
- **US-022** — As a user, I can see whether a resource supports preview, indexing, or execution.

## 11.4 Prompt library stories
- **US-023** — As a user, I can create a prompt and save it as a draft.
- **US-024** — As a user, I can save a reusable prompt blueprint/template.
- **US-025** — As a user, I can group prompts into galleries/collections.
- **US-026** — As a user, I can tag prompts and search them by tag, phase, or stack.
- **US-027** — As a user, I can see where a prompt was used, including time and repository context.
- **US-028** — As a user, I can version a prompt rather than overwriting it blindly.
- **US-029** — As a user, I can clone an existing prompt into a new prompt draft.

## 11.5 Prompt factory stories
- **US-030** — As a user, I can choose a project phase and get a recommended blueprint.
- **US-031** — As a user, I can build a prompt automatically from selected project metadata and linked resources.
- **US-032** — As a user, I can edit the auto-generated prompt before saving or sending it.
- **US-033** — As a user, I can save partially completed prompt work.
- **US-034** — As a user, I can export a generated prompt to clipboard or file.
- **US-035** — As a user, I can submit a prompt to a chosen provider profile when allowed.
- **US-036** — As a user, I can see validation warnings before submitting a prompt externally.

## 11.6 Validation stories
- **US-037** — As a reviewer, I can validate user stories and use cases.
- **US-038** — As a reviewer, I can validate ASCII layouts against stories and use cases.
- **US-039** — As a reviewer, I can validate architecture against requirements.
- **US-040** — As a reviewer, I can validate implementation plans against the approved architecture.
- **US-041** — As a reviewer, I can validate an initial prototype against the implementation plan.
- **US-042** — As a reviewer, I can prepare a test coverage plan and attach it to the project.
- **US-043** — As a reviewer, I can save findings, risks, and required follow-up actions.

## 11.7 Testing and evidence stories
- **US-044** — As a user, I can store screenshot evidence linked to a validation run.
- **US-045** — As a user, I can associate Playwright scenarios and test results with a project phase.
- **US-046** — As a user, I can distinguish planned tests, implemented tests, and executed results.
- **US-047** — As a user, I can keep UI tests and evidence associated with the exact feature or milestone they validate.

## 11.8 Extensibility stories
- **US-048** — As an architect, I can add a new resource type without redesigning the whole application.
- **US-049** — As an architect, I can add a new prompt phase/blueprint without rewriting existing flows.
- **US-050** — As an architect, I can split heavy modules into sidecar services later while preserving the UI model.

## 11.9 Workbench and orchestration stories
- **US-051** — As a user, I can open multiple project artifacts inside internal application tabs instead of opening many browser tabs.
- **US-052** — As a user, I can reorder, pin, close, and restore internal tabs according to my workflow.
- **US-053** — As a user, I can reopen the application after refresh, crash, or reconnect and recover the prior internal tab session.
- **US-054** — As a user, I can let inactive heavy tabs sleep while preserving enough state for fast restore.
- **US-055** — As a user, I can view the project structure as a canvas of phases, resources, prompts, validations, and tests.
- **US-056** — As a user, I can branch a new prompt or follow-up action from a specific prompt step represented in the project structure canvas.
- **US-057** — As a user, I can manage project events, milestones, reviews, and deadlines in a project calendar linked to project artifacts.
- **US-058** — As a user, I can open related prompts, resources, validations, or tests directly from the project structure canvas or calendar into internal tabs.

## 11.10 Development acceleration and tuning stories
- **US-059** — As a developer, I can see normalized watch, build, and runtime state for the running app instead of reading raw console output only.
- **US-060** — As a developer, I can wait for a trustworthy ready signal before starting Playwright or manual validation.
- **US-061** — As a developer, I can rely on short structured capsules attached to components and classes so implementation context stays current.
- **US-062** — As a developer, I can see which capsules are missing, stale, or malformed after source changes.
- **US-063** — As a developer, I can enable a dev-only tuning mode and target a specific component from the running UI.
- **US-064** — As a developer, I can attach a pasted screenshot and a short instruction to that targeted tuning request.
- **US-065** — As a developer, I can send that request through a local manager to Codex CLI and track its status.
- **US-066** — As a developer, I am notified only after the requested change is both applied and watch-ready again.
- **US-067** — As a delivery lead, I can inspect history tying a tuning request to changed files, watch readiness, and verification outcome.

## 12. Use cases

### UC-01 — Create workspace defaults
**Primary actor:** Workspace Owner  
**Precondition:** Application starts with no configured workspace  
**Main flow:**
1. Open settings.
2. Enter workspace defaults.
3. Configure database provider.
4. Configure provider profiles.
5. Save encrypted credentials.
6. Run health checks.
7. Confirm defaults.
**Success result:** Workspace baseline is ready for projects.

### UC-02 — Create a project
**Primary actor:** Architect / Developer  
**Main flow:**
1. Open project creation wizard.
2. Enter project name and description.
3. Enter dates and initial phases.
4. Select stack options and notes.
5. Save project.
**Success result:** Project workspace is created and visible in the dashboard.

### UC-03 — Add a typed resource
**Primary actor:** Architect / Developer / DevOps  
**Main flow:**
1. Open project resources.
2. Choose resource type.
3. Fill the type-specific form.
4. Reference any required secret profile.
5. Save.
6. Optionally validate and index.
**Success result:** Resource becomes visible in project workspace.

### UC-04 — Build an architecture prompt
**Primary actor:** Architect  
**Main flow:**
1. Open prompt factory.
2. Choose project and phase “Architecture”.
3. Select blueprint.
4. Select relevant resources and options.
5. Review assembled prompt.
6. Save and optionally send/export.
**Success result:** Prompt is stored and traceable.

### UC-05 — Review architecture against requirements
**Primary actor:** Reviewer  
**Main flow:**
1. Open validation center.
2. Choose target architecture artifact.
3. Choose requirement baseline.
4. Run review checklist.
5. Record findings.
**Success result:** Gap list and actions are stored.

### UC-06 — Record prompt usage with repo context
**Primary actor:** Developer  
**Main flow:**
1. Open prompt detail.
2. Mark prompt as used.
3. Select project and repository.
4. Enter branch / commit / note.
5. Save usage record.
**Success result:** Usage becomes part of project history.

### UC-07 — Add a new feature through the mini-cycle
**Primary actor:** Architect / Developer / Reviewer  
**Main flow:**
1. Open project feature cycle.
2. Enter feature summary.
3. Generate architecture prompt.
4. Generate plan prompt.
5. Generate implementation prompt.
6. Generate test plan prompt.
7. Record validations.
**Success result:** Feature work becomes a structured sub-cycle.

### UC-08 — Manage sensitive connector profiles
**Primary actor:** Workspace Owner / DevOps  
**Main flow:**
1. Create secret record.
2. Create FTP/SSH profile.
3. Reference secret.
4. Run connection test.
5. Save only encrypted storage.
**Success result:** Sensitive data is reusable and protected.

### UC-09 — Validate UI with screenshots and tests
**Primary actor:** QA / Developer  
**Main flow:**
1. Open test lab.
2. Link validation plan.
3. Attach Playwright suite and screenshots.
4. Record result.
5. Link findings to story/feature.
**Success result:** UI evidence and validation become traceable.

### UC-10 — Restore the internal workbench after interruption
**Primary actor:** Architect / Developer  
**Main flow:**
1. Work across several internal tabs.
2. Refresh, reconnect, or reopen the application.
3. Restore the previous tab session from persisted browser state.
4. Reopen active, background, and sleeping tabs safely.
5. Resume the previous task from the recovered active tab.
**Success result:** The workspace is recoverable without relying on many browser tabs.

### UC-11 — Manage project structure visually
**Primary actor:** Architect / Delivery Lead  
**Main flow:**
1. Open the project structure tab.
2. Add or select phases, resources, prompt sessions, validations, or tests.
3. Link nodes with meaningful relationships.
4. Open a related artifact from the canvas.
5. Save and later restore the structure state.
**Success result:** Complex project flow becomes visible and navigable.

### UC-12 — Coordinate work through the project calendar
**Primary actor:** Architect / QA / Delivery Lead  
**Main flow:**
1. Open the project calendar tab.
2. Create or update milestones, reviews, deadlines, or release events.
3. Link events to project phases or artifacts.
4. Open the related artifact from the calendar.
5. Persist and later restore the calendar state and preferred view.
**Success result:** Project scheduling and artifact navigation stay connected.

### UC-13 — Wait for the application to be truly ready after a change
**Primary actor:** Developer / Codex
**Main flow:**
1. Change source files or receive a Codex-generated patch.
2. Let the manager observe the new watch cycle.
3. Confirm build or hot reload progress from normalized watch state.
4. Confirm runtime readiness through the development endpoint in the main app.
5. Continue with Playwright or manual review only after the manager emits `Ready`.
**Success result:** Verification starts from a trustworthy runtime state instead of arbitrary sleeps.

### UC-14 — Keep capsule documentation aligned to source
**Primary actor:** Developer
**Main flow:**
1. Edit a component or class.
2. Update or add its capsule comment.
3. Let the manager regenerate capsule artifacts.
4. Review capsule coverage or drift if the structure is invalid or missing.
**Success result:** Codex-facing reference documentation stays near the real source state.

### UC-15 — Tune a component directly from the running UI
**Primary actor:** Developer / Architect
**Main flow:**
1. Enable tuning mode in development.
2. Click the tuning handle on a specific component.
3. Paste a screenshot and add a short instruction.
4. Submit the request through the local manager.
5. Wait for Codex completion, watch readiness, and optional verification.
6. Review the result in the app after the ready notification appears.
**Success result:** UI refinement becomes a short targeted loop instead of a manual multi-tool handoff.

## 13. Navigation expectations

## 13.1 Top-level navigation
- Dashboard
- Projects
- Prompt Gallery
- Prompt Factory
- Validation Center
- Test Lab
- Settings

## 13.2 Project-level navigation
- Overview
- Structure
- Calendar
- Stack Profile
- Resources
- Prompts
- Architecture
- Plan
- Validation
- Test Evidence
- Activity

## 13.3 Deep-link expectations
Every major item should be openable from:
- dashboard cards
- search results
- activity timeline
- related links inside detail pages

## 14. UX principles for the interface

1. **Project context first** — the current project and phase should always be visible.
2. **Type-aware editing** — different resource types need tailored forms, not generic JSON forms.
3. **Progressive disclosure** — advanced options should not bury the happy path.
4. **Auditability** — every generated prompt and validation result must be traceable.
5. **Sensitive-by-default design** — secret values should never be casually visible.
6. **Same pattern everywhere** — list + details + actions must remain consistent across modules.
7. **Fast reuse** — clone, duplicate, apply template, and save draft should be first-class actions.
8. **Human approval gates** — any action with external execution consequences must be explicit.
9. **Clear system state** — show whether something is saved, draft, validated, indexed, synced, or failed.
10. **Internal workbench over browser-tab sprawl** — high-density work must stay inside application-managed tabs.
11. **Recoverability** — refresh, reconnect, and crash recovery must feel intentional.
12. **Future-safe IA** — navigation must support growth without turning into tool sprawl.
13. **Fast but trustworthy iteration** — development acceleration features must reduce waiting without introducing false-ready states.
14. **Dev-only assistance is explicit** — tuning controls and manager-driven automation must remain visibly separate from normal product usage.

## 15. UX validation criteria

The UX is acceptable only if:
- a new user can create a project in one guided flow
- a user can add at least one resource of each required type
- the prompt factory can be used without manual copy-paste of project metadata
- prompt usage can be traced back to project/phase/time
- secrets are referenced, not scattered
- validation views connect requirements, outputs, and findings
- internal tabs can be reopened after interruption
- project structure and calendar views can open linked artifacts
- normalized watch readiness is visible and trustworthy during development
- capsule drift is visible before it becomes widespread documentation debt
- tuning mode can target a specific component without exposing unsafe controls in normal usage
- adding a new phase or resource type does not require a UI redesign
- the shell remains coherent even when more modules are added

## 16. UX risks

1. **Scope sprawl** — too many resource types may make forms inconsistent.
2. **Feature density** — the application can become “everything everywhere” without strong IA.
3. **False sense of automation** — users might assume generated prompts are inherently correct.
4. **Security fatigue** — too many safety dialogs can become ignored noise.
5. **Over-generic option modeling** — too much abstraction can harm discoverability.
6. **Under-designed activity history** — traceability can become unusable if not filtered and grouped well.
7. **Browser-tab overload** — without internal tabs, Interactive Server cost multiplies quickly.
8. **Canvas sprawl without conventions** — a structure canvas can become visually noisy unless node and link semantics are disciplined.
9. **False-ready automation** — a fast loop can become actively harmful if readiness is inferred from weak signals.
10. **Capsule drift** — compressed source documentation will decay if missing coverage is tolerated.
11. **Unsafe local automation** — tuning mode can become risky if it exposes Codex submission too casually.
12. **Self-triggering watch loops** — generated artifacts can create noisy rebuild cycles unless excluded deliberately.

## 17. UX mitigation actions

- Use a typed resource descriptor registry.
- Use phase-based dashboards and recommended actions.
- Keep one common page template for lists/details/forms.
- Use a universal right-side action panel for save/validate/export/send operations.
- Keep validation deterministic where possible and AI-assisted where useful.
- Build search and filters early.
- Implement project and prompt templates from the beginning.
- Build the internal tab model and restore policy intentionally instead of as ad hoc component state.
- Keep structure-canvas and calendar wrappers aligned to the documented JavaScript engines before attempting deeper rewrites.
- Use a dedicated local manager to normalize watch and runtime readiness instead of relying on raw console text alone.
- Require capsule coverage and drift visibility from the start.
- Keep tuning mode dev-only, explicit, and tied to local manager approvals and notifications.
- Exclude generated manager artifacts from rebuild loops.

## 18. Final UX conclusion

The required UX is best served by:
- a **workspace shell**
- a **project-centered flow**
- a **typed resource system**
- a **prompt library + prompt factory pair**
- a **validation center**
- a **test evidence area**
- a **generalized option model**
- a **consistent component system**
- a **developer acceleration loop with trustworthy readiness and targeted tuning**

This is sufficient to support the current scope and still leave space for future vertical and horizontal growth.
