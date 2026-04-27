# Structured Input

## Core Objective

- Make process-run prompts platform-neutral while adding specialized default agents and process scenarios for coding and non-coding workflows.

## Hard Constraints

- Move .NET/Blazor/calculator tactics out of the base prompt.
- Keep generic evidence, artifact, retry, and outcome contracts.
- Add default .NET, JavaScript, business, finance, and marketing agents.
- Add default process coverage for non-coding business-plan style work.
- Validate process execution with PostgreSQL, not SQLite.
- Attempt real-agent validation after atomic tests and mock-agent checks pass.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- `N001`: Base prompt is overfit to .NET/calculator work.
- `N002`: Specialized .NET architect/developer/QA agents are required.
- `N003`: Specialized JS architect/developer/QA agents are required.
- `N004`: Business strategist, financial strategist, and marketing specialist agents are required.
- `N005`: Default processes are required for non-coding tasks such as business planning.
- `N006`: Validation must be atomic first, then handoff/process-flow oriented.
- `N007`: PostgreSQL is required for process validation.

## Dependency And Sequencing Signals

- Prompt neutrality must land first because every real process run inherits it.
- Default agents must exist before new process templates can be staffed naturally.
- Template load/projection must pass before PostgreSQL process execution.
- Real-agent validation should only run after deterministic prompt, seed, and template tests pass.

## Validation Expectations

- Focused prompt tests for forbidden base-prompt phrases and required generic contract phrases.
- Seed catalog tests for new agents, managed seed refresh/fallback keys, and capability assignments.
- Template-pack tests for business-plan process loading/projection.
- PostgreSQL-backed process validation using existing test infrastructure.
- Real-agent scenario proof or a precise provider-credential blocker.

## UI Validation Strategy

- N/A for planned code/prompt/template work. If a later process validation exercises UI, record route, viewport, actions, screenshots, and result in `reviews/01-execution-report.md`.

## Browser Validation Analytics

- Seeded as N/A rows in `reviews/01-execution-report.md`; update only if a real browser validation is executed.

## Working Assumptions

- Existing seed/catalog/process-template architecture is the correct extension point.
- JavaScript specialization can start with instructions and existing workspace tools until a dedicated JS skill is available.
- Provider credentials may not be available locally; this does not block deterministic PostgreSQL and mock-agent validation.

## Primary Risks

- Removing too much prompt guidance can weaken implementation proof. The fix is to preserve generic evidence rules and put technology tactics on agents.
- Missing managed seed refresh keys can leave existing workspaces with old agent definitions.
- Invalid template JSON can break the whole process template pack.
