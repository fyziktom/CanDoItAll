# SB01 Semantic Invariants

## Git Wrapper Command Contract

- Invariant ID: `ProcessTemplateGitFoundationTests`
- Source raw note: `improve git wrapper` and `study it and based on it propose architecture improvements`.
- Expected behavior: Supported local git commands are represented as `GitCommandSpec` values built by `GitRepositoryCommandBuilder`, with `GitRepositoryClient` delegating to the same builder instead of duplicating argument grammar.
- Disallowed shallow implementation: A wrapper that shells out through ad hoc strings, accepts option-like branch or revision values, or permits `.git` metadata paths would not satisfy the requirement.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-wrapper-tests.txt` captured the initial broad test command failing before a meaningful result because the existing `CanDoItAll.Web` process locked build outputs.
- Passing test: `bundle://proof/SB01/transcripts/wrapper-focused-tests.txt` passed `ProcessTemplateGitFoundationTests` after the typed builder, typed revision/path validation, and client delegation were implemented.
- Changed source files: `repo://src/CanDoItAll.Git/GitRepositoryCommandBuilder.cs`, `repo://src/CanDoItAll.Git/GitRepositoryPath.cs`, and `repo://src/CanDoItAll.Git/GitRepositoryClient.cs`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` maps wrapper construction, validation, and downstream dependency claims to concrete source files.
- Red-team negative case: `ProcessTemplateGitFoundationTests` rejects `.git`, `.git/config`, `.Git/config`, option-like branch names, and option-like revisions before a command spec can be built.
- Downstream dependency check: SB02 command plans consume the same wrapper builder, and the SB04 focused test transcript re-runs wrapper and runtime git test fixtures together.
