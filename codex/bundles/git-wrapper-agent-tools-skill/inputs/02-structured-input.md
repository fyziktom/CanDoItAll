# Structured Input

## Core Objective

Create a maintainable local git operation layer for agents by improving the shared git wrapper, exposing bounded workspace git tools, and adding inline skill guidance in the template-backed capability system.

## Success Criteria

- `CanDoItAll.Git` is the single source for git command specs used by process code and agent workspace commands.
- App-managed agents can call bounded local git tools for status, diff, log, show, add, unstage, commit, create branch, and switch branch.
- Mutation git tools are policy-classified as mutation operations, require approval by default, and are not granted to read-only agents.
- Default agent templates that already receive git tools also receive the git operations skill.
- Focused tests and final affected test runs pass.

## Hard Constraints

- No remote/network git tools: push, pull, fetch, clone, remote, credential helpers, or authentication.
- No destructive history tools: reset, checkout, rebase, clean, branch delete, force operations.
- No silent fallback behavior.
- No shell-built git commands.
- No UI changes.

## Allowed Side Effects

- Production C# under `CanDoItAll.Git`, `CanDoItAll.AgentFramework.Core`, `CanDoItAll.AgentFramework.Maf`, and `CanDoItAll.AgentFramework.Models`.
- Template JSON and markdown under `Templates/Capabilities` and relevant `Templates/Agents/**/skills.json`.
- Focused unit/integration tests.
- Bundle proof artifacts under `codex/bundles/git-wrapper-agent-tools-skill/proof`.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`

## Input Coverage Signals

- "improve git wrapper" owns the shared command-spec architecture, validation, and tests.
- "create with it set of tools for agents" owns runtime tool methods, policy metadata, and tool descriptors.
- "complementary skill" owns the inline skill capability and default agent assignments.
- "study it and based on it propose architecture improvements" requires source-backed current-state and architecture proof before implementation closure.

## Dependency And Sequencing Signals

- Wrapper command specs must land before runtime tools consume them.
- Runtime tool names and policy metadata must land before skill instructions can safely name tools.
- Template materialization must pass before default agent assignments can be closed.

## Validation Expectations

- Focused tests for wrapper specs, workspace command plans, access mapping, runtime tool composition, template materialization, and assignment validation.
- Source assertions showing each tool is present across constants, command service, MAF plugin, capability templates, and relevant agent assignments.
- Anti-stub audit showing no fake git implementation, TODO, NotImplemented, or fixture-only branches in production flow.

## Evidence Contract

- `proof/SBxx/transcripts/*.txt` for focused commands.
- `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` for critical subbundles.
- Final `reviews/01-execution-report.md` rows for gates and raw note closure.

## UI Validation Strategy

- N/A - non-UI runtime/tooling change.

## Browser Validation Analytics

- N/A - no browser-visible surface.

## Working Assumptions

- Standard git operations means bounded local workflow operations, not remote publishing or destructive history operations.
- `CanDoItAll.AgentFramework.Core` may reference `CanDoItAll.Git` because the git project is a small command-spec boundary with no dependency on higher layers.

## Primary Risks

- A tool can be added in one layer but omitted in capability templates or access policy.
- Mutation git tools can be accidentally treated as read-only.
- Skill instructions can drift from the actual shipped runtime tools.
