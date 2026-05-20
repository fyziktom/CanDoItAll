# Structured Input

## Core Objective

- Bring the repository documentation to a public-version baseline that reflects the current modular structure, local runtime dependencies, install/setup scripts, and per-project documentation coverage.

## Success Criteria

- The root README explains local development with PostgreSQL and Qdrant from Docker or native PostgreSQL.
- The root README documents the web app install script, MCP resetup script, and Codex skill installer.
- Documentation indexes point readers to current setup, API, MCP transition, and project-level docs.
- Every tracked `.csproj` directory under `src`, `tests`, and `tools` has a `README.md`.
- New or refactored areas missing docs, especially Cognitive Memory, Scheduler Planner, Plugins, Voice, Charts, Mermaid, document tools, bundled plugins, and Mermaid MCP tests, have project-level READMEs.
- Stale guidance is either removed or explicitly marked as retired/suppressed when it describes old MCP paths.

## Hard Constraints

- Keep the changes documentation-only.
- Do not reintroduce retired `candoitall_processes` or `candoitall_projectstructure` setup paths.
- Keep setup guidance source-grounded in scripts and current configuration.
- Keep project READMEs concise enough to stay maintainable.

## Allowed Side Effects

- Markdown documentation under repository docs, project directories, and the bundle itself.
- No runtime code, project files, migrations, or generated binaries.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- `N001`: Docs are not up to date and miss new or refactored modules.
- `N002`: Main README must explain PostgreSQL and Qdrant setup and running.
- `N003`: Main README must explain installation script, MCP script, and skill script.
- `N004`: Each project must have its own README.
- `N005`: Remove old things and prepare docs for a soon public version.

## Dependency And Sequencing Signals

- The project and script inventory must happen before editing docs, or the public README can become another stale summary.
- Runtime/setup docs should be updated before project README coverage is closed, because several project READMEs point to the root setup flow.
- Final validation depends on proving all `.csproj` directories have README coverage.

## Validation Expectations

- Run the bundle validator at prepared and completed stages.
- Run a project README coverage check comparing tracked `.csproj` directories against sibling `README.md` files.
- Run `dotnet build CanDoItAll.slnx --no-restore` or explain any build blocker if restore/build is unavailable.
- Inspect documentation diffs for stale script names, retired MCP paths, and inaccurate setup claims.

## Evidence Contract

- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --profile initiative --stage prepared codex\bundles\docs-public-readiness`
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --profile initiative --stage completed codex\bundles\docs-public-readiness`
- PowerShell project README coverage check.
- `dotnet build CanDoItAll.slnx --no-restore`

## UI Validation Strategy

- N/A - documentation-only change, no browser-visible product behavior.

## Browser Validation Analytics

- Record `N/A` rows in the execution report for each subbundle because the proof is repository/file validation, not rendered UI validation.

## Working Assumptions

- Public-version readiness means accurate contributor/operator docs, not a marketing rewrite.
- Existing historical bundle folders can stay because they are execution traceability, not public setup guidance.
- Project README coverage applies to tracked `.csproj` directories under `src`, `tests`, and `tools`; generated `bin`/`obj` content is out of scope.

## Primary Risks

- The solution references sibling repositories (`CanDoItAll.AgentFramework.Rag`, `CanDoItAll.AgentFramework.SemanticCompletion`, and code analytics projects); build validation can fail if those siblings are missing or stale.
- Docs can over-promise runtime maturity. The update must distinguish current PostgreSQL-first development from retained SQLite support.
- MCP docs can accidentally revive removed Process/ProjectStructure MCP servers; keep those paths marked retired.
