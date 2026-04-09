# AgentFramework Integration Simulation Friction

Date: 2026-04-09

## Seeded Scenario

- Active managed profile root: `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\fe8c1138e1b541cc97a32dbead3a2394`
- Project: `CanDoItAll.AgentFramework Integration Program`
- Project id: `eaee1691-f5cf-49b1-a43d-1c8cd07d50f0`
- Project processes route: `/projects/eaee1691-f5cf-49b1-a43d-1c8cd07d50f0/processes`
- Project structure route: `/projects/eaee1691-f5cf-49b1-a43d-1c8cd07d50f0/structure`

Seeded project-scoped processes:

1. `AgentFramework integration / role-first operating model baseline`
2. `AgentFramework integration / canonical model and boundary convergence`
3. `AgentFramework integration / local-LLM-safe execution slices`
4. `AgentFramework integration / OpenAI-assisted complex integration lane`
5. `AgentFramework integration / validation, rollout, and learning loop`

The simulation also seeded:

- realistic people, orgs, org-unit, and AI-agent parties
- project-level party assignments
- five process runs with mixed active and blocked states
- project graph nodes bound to project-scoped process routes
- repository, runtime, AI-lane, validation, and learning nodes

## Real Problems Observed

### 1. Project structure MCP is still pinned to a stale fixed port

The project-structure MCP tools failed because they still target `127.0.0.1:5032`, while the actual watch runtime for this session was at `http://127.0.0.1:5501`.

Impact:

- project-structure MCP could not be used for validation during this simulation
- seeding and validation had to bypass that MCP path

Needed improvement:

- project-structure MCP should resolve the active managed app session dynamically, or at minimum follow the same runtime-discovery pattern already used elsewhere

### 2. There is no public API for binding project nodes to process definitions or runs

To make project structure nodes open the correct process workspace, the seeder had to write `Workbench_ProjectNodeBindings` directly through `AppDbContext`.

Impact:

- process-to-project-graph integration is not first-class
- safe tooling cannot stay purely on canonical services
- future automation will keep re-implementing binding persistence details

Needed improvement:

- add a canonical service/API for binding a project node to a process definition or process run
- include explicit artifact kind and route update behavior in that API

### 3. Project-node bindings and metadata do not model process attachments explicitly

There is no first-class reference kind or typed metadata contract for process definition ids, process run ids, or process assignment semantics on project nodes.

Impact:

- process attachments are effectively route hacks
- downstream tooling cannot reason about process bindings structurally
- graph validation cannot distinguish a normal route from a process route

Needed improvement:

- add typed process binding metadata and dedicated reference kinds for process definition and process run

### 4. Process lane policy is stored only as free text

The simulation needed to encode:

- what a role is allowed to do
- what it cannot do
- when it must escalate
- whether work belongs to local LLM, OpenAI, or human-only lanes

Today those constraints live only inside role `Purpose`, `StaffingIntent`, `SnapshotSummary`, and step decision/exception summaries.

Impact:

- no machine-readable validation of lane rules
- hard to filter or audit which steps are local-safe versus external-model-safe
- difficult to generate consistent UX affordances from the model

Needed improvement:

- add structured process-role and process-step policy fields for:
  - allowed actions
  - prohibited actions
  - required escalation triggers
  - execution lane classification
  - sanitization requirement

### 5. Project party assignment roles are too coarse for reliable auto-binding

The current project assignment roles can say `TeamMember` or `Reviewer`, but that is too coarse to distinguish:

- canonical steward
- security reviewer
- QA reviewer
- architect
- workbench engineer

The simulation therefore had to start runs and then resolve role assignments explicitly for each process role.

Impact:

- default prebinding is too weak for realistic process execution
- project-wide staffing cannot express rich role-first staffing semantics

Needed improvement:

- support finer-grained preferred project assignment semantics or typed role-template matching
- allow auto-binding against reusable role definitions instead of only broad assignment categories

### 6. Process definition list metrics are inflated across versions

After updating and publishing the seeded definitions multiple times during seeding, the process MCP list showed inflated `roleCount` and `stepCount` values. The counts were clearly larger than the current active version.

Impact:

- process inventory views are misleading after normal versioned edits
- validation dashboards may overstate process complexity

Likely cause:

- list metrics are aggregating roles and steps across all definition versions instead of only the active or working version

Needed improvement:

- fix process list/read models so counts are derived from the current relevant version only

### 7. Workbench link rules are valid but not ergonomic enough for automation

The simulation initially tried to create explicit `Contains` links between parent and child nodes. Workbench rejected those with:

`Hierarchy links must be created through the explicit parent relationship.`

The rule is correct, but the failure happened only at runtime.

Impact:

- automation authors can easily choose an invalid link kind for hierarchy semantics
- graph authoring is harder than it needs to be

Needed improvement:

- expose or document a clearer supported-link matrix for project graph automation
- optionally add a helper that translates attempted containment links into parent/child guidance

### 8. Playwright MCP could not run in this environment

Playwright MCP failed with an `EPERM` when it tried to create `.playwright-mcp` under the protected WindowsApps install path.

Impact:

- browser validation had to fall back to raw HTTP checks instead of full UI interaction
- this would block real compactness / layout / right-click workflow validation in practice

Needed improvement:

- move Playwright MCP state to a writable per-user location
- validate the desktop packaging so browser tooling does not rely on a protected install directory

### 9. Managed-profile runtime lock is visible but there is no explicit seeding mode

The seeder ran successfully while the active profile reported `RuntimeLocked=True`, but there is no explicit managed-profile maintenance mode or seeding transaction workflow.

Impact:

- safe simulation or migration work against a live managed profile depends on optimistic SQLite behavior
- there is no first-class “prepare profile for bulk seed/migration” workflow

Needed improvement:

- add an explicit maintenance or seeding mode for managed profiles
- expose “profile write-safe” state to tools before bulk scenario loading

### 10. Browser validation for seeded scenarios is easy to misroute into an isolated test database

The Playwright test fixture defaults to spinning up its own temporary managed profile unless `CANDOITALL_PLAYWRIGHT_BASEURL` is explicitly pointed at an already running app. That default is correct for normal regression tests, but it produced a false-negative during seeded-scenario validation because the seeded AgentFramework project only existed in the active managed runtime.

Impact:

- seeded browser validation can silently run against the wrong database
- failures look like missing UI or broken routing even when the real managed app is correct
- the correct validation path depends on internal fixture knowledge

Needed improvement:

- add an explicit seeded-runtime validation mode for Playwright tests
- surface the active runtime base URL and profile identity in the browser harness output
- document or automate the `CANDOITALL_PLAYWRIGHT_BASEURL` handoff for managed-scenario proofs

### 11. Managed test-operation queue can get stuck in `Queued`

During this validation pass, `candoitall_tests_run` stopped accepting new work because an older operation remained in `Queued` indefinitely (`op_078afa840961472da3854d4630b12b8f`). The proof had to fall back to direct `dotnet test`.

Impact:

- MCP-based validation becomes unreliable even though the runtime itself is healthy
- Codex has to drop out of the managed-operation workflow and use shell fallback
- there is no obvious cancel/reset tool for a stale queued build/test operation

Needed improvement:

- add queue inspection and cancellation for managed build/test operations
- auto-expire or repair operations stuck in `Queued`
- include a clearer error payload when `candoitall_tests_run` rejects work because of queue state

## Follow-up Bundle Candidates

1. Add canonical process-to-project binding APIs and typed metadata/reference kinds.
2. Add structured execution-lane policy fields to processes and process roles.
3. Improve project-role matching so process auto-binding can use role templates instead of coarse categories.
4. Fix process inventory metrics to count only the active version.
5. Make project-structure MCP resolve the active managed runtime instead of a stale fixed port.
6. Repair Playwright MCP writable-state handling for desktop sessions.
7. Add a managed-profile maintenance mode for bulk seeding, migration, and replay scenarios.
8. Add a seeded-runtime Playwright validation mode that targets the active managed app/profile explicitly.
9. Add recovery and cancellation for stuck managed build/test operations.
