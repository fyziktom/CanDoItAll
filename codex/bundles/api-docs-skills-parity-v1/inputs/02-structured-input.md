# Structured Input

## Core Objective

- Repair documentation, API contract coverage, repo-managed API skills, active local skill copies, and agent runtime tool surfaces so they match the current CanDoItAll control-plane implementation.

## Success Criteria

- Source-derived route inventory is captured in XLSX and kept current through implementation.
- Missing, obsolete, and stale docs/skills/API coverage is mapped to concrete subbundles and requirements.
- Cognitive Memory docs and tests reflect 38 routes per surface and the v1 alias surface.
- Agent, Workflow, Process, Project Structure, and Cognitive Memory API skills include exact route and DTO guidance.
- Provider capability and model-parameter behavior is documented where it affects agents and APIs.
- Process and Project Structure agent tool parity is either implemented or explicitly documented as HTTP-only fallback.
- Drift guardrails exist so new route changes do not silently bypass docs, skills, and test coverage.

## Hard Constraints

- Preserve raw input and source evidence.
- Use strongly typed DTO/source references; avoid stringly-typed repair guidance where source types exist.
- Do not invent fallback mechanisms that hide route, DTO, or docs drift.
- Keep implementation phases small and gate downstream work on proof from earlier phases.
- Synchronize active skill copies under `C:\Users\lucys\.codex\skills` after repo skill edits.

## Allowed Side Effects

- Docs under `docs/`.
- Repo-managed skills under `codex/skills/`.
- API contract and focused API tests when routes are missing from OpenAPI/test assertions.
- Agent runtime tools and tool policy where parity gaps are implemented instead of documented as HTTP-only.
- Validation scripts/tests and generated inventory artifacts.

## Source Artifacts

- `inputs/01-source-artifacts.md` lists the inspected source, docs, skills, generated workbook, and CodeAnalytics snapshot.

## Input Coverage Signals

- Agents surface: teams, providers, capabilities, chat, execution runs, approvals, artifacts, metrics, runtime snapshots.
- Providers surface: private provider flags, pricing, tags, feature matrix, hosted/local tools, structured output, reasoning effort.
- Workflows surface: definitions, components, test/runtime runs, artifacts, pending requests, analytics, pagination, source process linkage.
- Processes surface: definitions, launches, live runs, step transitions, artifacts, assignments, escalations, approvals, direct messages, templates.
- Project Structure surface: project hierarchy, nodes, metadata/status/progress/markers/priority, process/workflow commands, assets, leases, analytics.
- Cognitive Memory surface: legacy and v1 bases, contract route, database transfer, settings, ingestion, recall/review, projections, automation, retention, advanced/professor/distributed operations.
- Docs and skills: route lists, DTO maps, examples, historical docs, active skill sync.

## Dependency And Sequencing Signals

- The source inventory subbundle blocks all later work because stale route counts would invalidate docs, skill, and test repairs.
- API contract repairs block docs/skills closure where route behavior or OpenAPI visibility is uncertain.
- Agent tool parity must be decided before skills claim that agents can execute process or project-structure operations directly.
- Docs and skills must be refreshed before drift guardrails can encode route coverage expectations.
- Final closure must not start until hash sync proof and focused test proof exist.

## Validation Expectations

- Prepared-stage bundle validation passes before implementation starts.
- Each subbundle records commands, changed files, and proof in `reviews/01-execution-report.md`.
- Focused API and tool tests pass for changed behavior.
- Workbook is regenerated after route/DTO/tool changes.
- Completed-stage bundle validation passes before final handoff.

## Evidence Contract

- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\api-docs-skills-parity-v1 --profile initiative --stage prepared`
- `node .codex\tmp\api-docs-skills-gap-map\build-gap-map.mjs`
- Focused `dotnet test` commands for API OpenAPI route coverage and agent tool policy/runtime changes.
- `git diff --check` for markdown/source formatting.
- `Get-FileHash` proof for repo skills and active local skill copies.

## UI Validation Strategy

- Most work is docs/API/tooling and does not require browser proof.
- If a subbundle changes Blazor UI or visible provider/process/project screens, that subbundle must add a maximized browser pass, screenshot review, and narrower viewport follow-up before closure.

## Browser Validation Analytics

- Default value is `N/A` for non-UI subbundles.
- UI-affecting subbundles must log route/window, viewport, Playwright actions/assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- The current code is the source of truth; docs and skills should conform to code unless a subbundle proves the code contract is wrong.
- Existing repo-managed skills are intended to stay synchronized with active local skill copies.
- Plugin and Projects API skill coverage is undecided and must be made explicit rather than assumed.

## Primary Risks

- Route inventory drift during implementation can invalidate downstream docs and skills.
- Adding process/project runtime tools without policy and approval coverage can create security and orchestration risks.
- Updating only repo skills without active local sync leaves agents using stale guidance.
- Over-documenting historical proof files as living guidance can mislead future operators.
