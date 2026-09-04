# Normalized requirements

## Baseline and governance

- **R-001** — Reference and comply with `CDA-UI-SEAMS-BASE-v1`.
- **R-002** — Refresh target/base branches before editing; recorded SHAs are not pins.
- **R-003** — Preserve live sibling source mode for Components and FileTools.
- **R-004** — Execute sequential subbundles with explicit dependency/reopen evidence.
- **R-005** — Use English comments in source code and scripts.

## Scope and compatibility

- **R-010** — Limit production scope to the Agents home/catalog/details seam and required
  DI registration/new same-project seam types.
- **R-011** — Preserve component locations and existing project references.
- **R-012** — Preserve `/agents`, all current query keys, compatibility behavior, labels,
  order, and visible semantics.
- **R-013** — Exclude provider-panel, workflow, voice, governance, diagnostics, Manager,
  dotnet-watch, sibling-repo, redesign, and physical-extraction work.

## State ownership

- **R-020** — Introduce stable `AgentWorkspaceSection` and `AgentsWorkspaceState`.
- **R-021** — Keep current `AgentWorkspaceTabs`/route codec as compatibility mapping.
- **R-022** — Make `AgentsHomePage` the single owner of route-significant section,
  selected agent/team, Simple Chat, usage, and details target state.
- **R-023** — Introduce stable `AgentDetailsSection` with explicit order mapping.
- **R-024** — Ensure child components emit typed state changes/intents and do not construct
  page URLs.

## Dependency seams

- **R-030** — Remove direct EF access and dashboard aggregation from `AgentsHomePage` via
  `IAgentsOverviewQuery`.
- **R-031** — Introduce one `IAgentCatalogController` for catalog data/repair/mutations.
- **R-032** — Make `AgentCatalogPanel` a controlled view with no feature-service injection.
- **R-033** — Move all route-significant catalog host actions to `AgentsHomePage`.
- **R-034** — Introduce one `IAgentEditorController` and explicit `AgentEditorSession`.
- **R-035** — Remove direct Workspace/provider/Projects/Secrets/infrastructure/persistence
  dependencies from `AgentDetailsDialog`.
- **R-036** — Preserve partial provider/secret and lazy-project error semantics.
- **R-037** — Do not introduce a fourth production interface without approved pattern
  decision; do not use a service bag or generic lifecycle base.
- **R-038** — Add no new partial class file.

## Behavioral preservation

- **R-040** — Preserve deep-link requested-agent open-once behavior at the page boundary.
- **R-041** — Preserve exact managed-agent identity protections and chat behavior.
- **R-042** — Preserve agent/team create/edit/delete and selection/result semantics.
- **R-043** — Preserve all ten agent-details sections and editor workflows.
- **R-044** — Preserve save normalization, capability, project/workspace/storage/secret,
  thinking-effort, approval, voice, and avatar behavior.
- **R-045** — Protect stale async completion where loads can overlap.

## Testability and proof

- **R-050** — Preserve or replace the 46 primary component behavior cases; document any
  intentional discovery change before implementation.
- **R-051** — Keep the 10 current route-state cases green without URL changes.
- **R-052** — Remove private reflection, private-method invocation, numeric-tab seeding,
  and uninitialized concrete services from target test harnesses.
- **R-053** — Add direct durable tests for typed state, overview query, catalog controller,
  editor controller, and forbidden dependency boundaries.
- **R-054** — Rewrite the adjacent `WorkflowsPageTests` private `OpenWorkflows` reflection
  case through public UI/navigation behavior.
- **R-055** — Do not add tests that freeze filenames, private members, exact dependency
  counts, partial counts, or one implementation syntax.
- **R-056** — Run focused discovery/tests, final stable gate, portability-static gate,
  architecture gate, and large-desktop host smoke.
- **R-057** — Record route, sandbox, and project-extraction readiness and remaining
  coupling at closure.
