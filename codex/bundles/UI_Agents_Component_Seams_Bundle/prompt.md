# Primary execution prompt

Execute `CDA-UI-SEAMS-AGENTS-01-v1` on the current
`fyziktom/CanDoItAll:components-decoupling` branch.

## Mandatory reading order

1. root `AGENTS.md`;
2. `.github/copilot-instructions.md`;
3. `docs/testing.md`;
4. `codex/bundles/UI_Component_Seams_Shared_Architecture_Bundle/README.md`;
5. the shared-base architecture documents and child-bundle contract;
6. this bundle's root README, requirements, architecture, inventories, phase plan, and
   current subbundle README;
7. current source and tests at the refreshed branch head.

Use the repository's bundle execution and C# architecture skills. Use CodeAnalytics as a
read-only evidence source when available and record the snapshot/finding gap when it is
not available.

## Execution contract

- Refresh `components-decoupling` and `development`; do not assume the recorded SHAs are
  pins.
- Confirm the branch still contains `CDA-UI-SEAMS-BASE-v1` and is not behind development.
- Execute subbundles sequentially. Do not combine or parallelize them.
- Preserve existing component locations, URLs, query keys, visual behavior, and live
  sibling source dependencies.
- Make responsibility leave the Razor component; do not merely hide it in forwarding
  methods or another partial file.
- Use only the three planned production seams unless a written pattern-decision addendum
  is approved: `IAgentsOverviewQuery`, `IAgentCatalogController`, and
  `IAgentEditorController`.
- Keep pure state mapping, filtering, normalization, and selection rules as ordinary
  top-level types without interfaces.
- Keep navigation, dialogs, notifications, and managed-chat launch at the owning page or
  top-level editor boundary; lower controllers return typed results and do not present UI.
- Do not introduce `IServiceProvider`, direct EF access in Razor, service bags, generic
  lifecycle bases, feature references in `AppComponents`, or new partial files.
- Replace private-reflection and uninitialized-service test harnesses with public
  parameters, typed state, intents, controller fakes, and direct controller tests.
- Preserve behavior coverage; do not inflate the suite with tests of filenames, counts,
  private member names, or prescribed syntax.
- All source-code and script comments must be English.
- Do not push, merge, publish, or perform other remote writes without explicit approval.

## Required progress evidence

For every subbundle:

- record the refreshed source SHA and dirty state;
- state owned requirements and files changed;
- run `--list-tests` for the exact focused filter and confirm expected discovery;
- record failing-first evidence when the subbundle introduces a new behavior/boundary;
- run the focused tests/checks and production build;
- update the execution report, requirement closure, and architecture checkpoint;
- stop if the prerequisite boundary is incomplete or a later phase would rely on a fake
  separation.

The final closure must run the named stable and portability gates because this bundle
changes shared AgentFramework UI composition/DI. The browser proof is a direct large-
desktop host smoke, not a reason to expand or unquarantine unrelated Playwright tests.

## Stop rules

Stop and report rather than improvising when:

- branch movement changes the target components or tests materially;
- a fourth production interface appears necessary;
- preserving behavior requires a new route/query key;
- `AgentCatalogPanel` cannot become controlled without moving files/projects;
- `AgentDetailsDialog` still needs direct production services after the planned controller;
- controller tests require the full web host and no coherent smaller seam is visible;
- an obsolete source-shape test blocks a valid simplification;
- a dependency cycle or new project reference appears;
- focused baseline tests fail before implementation for unrelated reasons.

## Required final report

Use `reviews/execution-report-template.md`. Include exact commands, expected/actual test
discovery, results, files changed, responsibilities removed, dependencies before/after,
remaining coupling, route/sandbox/project-extraction readiness, deviations, skips, and
residual risks.
