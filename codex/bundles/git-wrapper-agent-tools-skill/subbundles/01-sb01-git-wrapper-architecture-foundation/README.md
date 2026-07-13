# SB01-git-wrapper-architecture-foundation

## Status

- `Completed`

## Objective

Harden and refactor `CanDoItAll.Git` so it is the reusable command-spec authority for local git operations.

## Success Criteria

- Git command specs can be created without executing git.
- Existing `GitRepositoryClient` behavior uses the same spec builder.
- Path, branch, and revision validation reject unsafe option-like or `.git` inputs.
- Standard local operations needed by later tools are represented by typed specs.

## Covered Inputs

- REQ-001
- REQ-002
- REQ-006
- REQ-007

## Prerequisites

- Prepared bundle validator passes.
- Current source still matches `bundle://analysis/01-current-state.md`.

## Exact Source References

- `repo://src/CanDoItAll.Git/GitCommandContracts.cs`
- `repo://src/CanDoItAll.Git/GitRepositoryClient.cs`
- `repo://src/CanDoItAll.Git/GitRepositoryPath.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessTemplateGitFoundationTests.cs`

## Deliverables

- Reusable git command-spec builder.
- Wrapper support for status, diff, log, show, add, unstage, commit, branch create, and switch.
- Typed validation for branch names, revisions, path specs, and diff modes.
- Focused unit tests for command argument order, sanitization, and negative validation.

## Dependency Impact

- SB02 depends on this subbundle for every runtime tool command plan.
- If SB01 is wrong, later tool receipts can look valid while executing the wrong git operation.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, `proof/SB01/manifest.md`, and `proof/SB01/semantic-invariants.md`.

## Implementation Steps

1. Add a reusable `CanDoItAll.Git` command-spec builder and route `GitRepositoryClient` through it.
2. Add missing typed wrapper operations for unstage and any command shapes required by runtime tools.
3. Harden `GitPathSpec`, `GitPathAuthorizer`, `GitBranchName`, and revision inputs against unsafe values.
4. Update existing wrapper tests and add negative-path tests.
5. Capture source assertions, command transcript, changed-file hashes, and anti-stub audit output under `proof/SB01/`.

## Scope Exceptions

- Do not expose merge, abort merge, or conflict-resolution tools to agents in this subbundle.
- Do not add remote or destructive operations.

## Do Not Do

- Do not use shell strings or string-concatenated commands.
- Do not add fallback command construction in the runtime layer.
- Do not weaken existing commit-message sanitization.

## Acceptance Checklist

- `GitRepositoryClient` no longer owns duplicate command grammar separate from the reusable spec builder.
- `.git`, `.git/...`, case variants, outside-root paths, option-like branch names, and option-like revisions are rejected.
- Commit message remains masked in `SanitizedCommand`.
- Wrapper tests pass.

## Proof Required

- Focused command transcript: `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter ProcessTemplateGitFoundationTests`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- `bundle://proof/SB01/source-assertions.md`
- `bundle://proof/SB01/anti-stub-audit.txt`

## Browser Validation Logging

- N/A - no browser-visible or host-visible UI behavior.

## Progression Gate

- SB02 may start only after focused wrapper tests pass.
- The SB01 proof manifest must cite existing transcript, source assertion, semantic invariant, anti-stub, and changed-file hash artifacts.

## Suggested Agent Prompt

```text
Implement SB01 only. Refactor the shared git wrapper into a reusable command-spec authority, preserve existing behavior, add typed validation, prove the command shapes and negative cases with focused tests, then write the SB01 proof manifest and execution-report row. Stop before runtime tool exposure.
```
