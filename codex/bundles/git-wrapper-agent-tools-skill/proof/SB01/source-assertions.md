# SB01 Source Assertions

## Architect Note Mapping

| Note | Source proof |
| --- | --- |
| `we already have some basic wrapper. study it` | Current-state analysis identified the original wrapper in `src/CanDoItAll.Git/GitCommandContracts.cs`, `GitRepositoryClient.cs`, `GitRepositoryPath.cs`, and `DefaultGitCommandExecutor.cs`. |
| `based on it propose architecture improvements` | `architecture/01-target-solution.md` selected `CanDoItAll.Git` as the command-spec authority and rejected duplicate runtime command grammar. |
| `improve git wrapper` | `GitRepositoryCommandBuilder` centralizes command specs, `GitRepositoryClient` delegates to it, and typed validation now covers path, branch, and revision inputs. |
| `standard operations with git` | Builder and client support status, diff, log, show, add, unstage, commit, branch create, and switch; merge support remains wrapper-only and is not exposed as an agent tool in SB02. |

## Behavioral Assertions

- Exact command sequences are asserted in `ProcessTemplateGitFoundationTests`.
- Commit-message masking is asserted through `GitCommandSpec.SanitizedCommand`.
- Unsafe metadata and option-like inputs are rejected before execution.
- No runtime tool behavior was changed in SB01; that is intentionally deferred to SB02.
