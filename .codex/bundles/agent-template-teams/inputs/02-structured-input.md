# Structured Input

## Core Objective

- Replace source-hardcoded default agent definitions with editable templates under `Templates/Agents`, grouped into teams and loaded by the seed system.

## Success Criteria

- Every default agent has a folder containing `instructions.md`, `settings.json`, and `skills.json`.
- Team folders contain team metadata/settings plus member folders.
- Seeded default agents and agent teams are produced from the template pack.
- Previous hardcoded default agent instruction assets and definition literals are removed or reduced to template materialization logic.
- Regression tests prove templates load and seed into expected teams.
- Browser validation proves the default agents still appear and remain usable at the app surface.

## Hard Constraints

- Keep generic editable information in simple files, not C# string literals.
- Do not leave obsolete hardcoded default-agent definitions in source once the new path works.
- Preserve existing default agent keys, capabilities, providers, and behavior as closely as possible.
- Use Playwright/browser validation before closure.

## Allowed Side Effects

- Add `Templates/Agents` and update `Templates/README.md`.
- Add a persistence loader for the template pack.
- Update seed and normalizer code to consume file-backed templates and team definitions.
- Update tests whose assumptions change because default teams now seed automatically.

## Source Artifacts

- `C:\repositories\CanDoItAll\Templates`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`

## Input Coverage Signals

- Raw note N001: agent templates should mirror the existing Templates workflow/process pattern.
- Raw note N002: each agent needs its own folder with instructions, skills, and JSON settings.
- Raw note N003: default agents must be split into teams with team info/settings.
- Raw note N004: each agent instruction set must be reviewed and improved.
- Raw note N005: hardcoded default agents must be removed after the new system is proven.
- Raw note N006: Playwright/browser validation must assure agents work as before.

## Dependency And Sequencing Signals

- The loader and template pack must exist before seed migration can be trusted.
- Seed migration must be proven before removing embedded instruction assets.
- Browser validation depends on build/test proof and a runnable local app.

## Validation Expectations

- Build the affected persistence project.
- Run targeted integration tests for template loading, seed normalization, provider/access preservation, and agent runtime compatibility.
- Audit source for removed hardcoded default-agent assets.
- Use browser automation against the local app agent surface.

## Evidence Contract

- Command transcripts under `proof/SBxx/transcripts`.
- Bundle validator output for prepared and completed stages.
- Test command output with exit code 0.
- Browser screenshots or assertions recorded in `reviews/01-execution-report.md`.

## UI Validation Strategy

- Open the local app agent catalog/agents route after the seed migration.
- Validate desktop and narrower viewport rendering where practical.
- Confirm expected default team or agent names are visible and no obvious errors are shown.

## Browser Validation Analytics

- Record route, viewport, browser actions/assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- The current default agent set in the seed builder is the source of truth for initial template export.
- Existing provider and capability catalogs remain valid.
- Runtime-created agents and test helper agents are not default templates and may still construct `AgentDefinition` in code.

## Primary Risks

- Template JSON shape may diverge from domain model expectations.
- Existing data normalization may fail to add or refresh seeded teams.
- Browser validation may require local app dependencies or seed state that differs from integration tests.
