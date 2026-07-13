# SB01 Proof Manifest

## Scope

- Subbundle: `SB01-git-wrapper-architecture-foundation`
- Status: `Completed`
- Closure date: `2026-06-29`

## Portable References

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Source assertion map: `bundle://proof/SB01/source-assertions.md`
- Primary wrapper source: `repo://src/CanDoItAll.Git/GitRepositoryCommandBuilder.cs`

## Changed Files

| File | SHA-256 |
| --- | --- |
| `src/CanDoItAll.Git/GitRepositoryPath.cs` | `7BB90E3B3307BCE1F00DACB38C2ADA722B2837DD0B3A9BD999535C8108634F9D` |
| `src/CanDoItAll.Git/GitRepositoryClient.cs` | `7838E29B92E116504E744B68D50413FA5355D5253766B678FE4D58595132A7E3` |
| `src/CanDoItAll.Git/GitRepositoryCommandBuilder.cs` | `A09E2D06EB587B44C4AF55A8AB7AD3785101719BAE7E8CCF5E133D478199C633` |
| `tests/CanDoItAll.Tests.Unit/ProcessTemplateGitFoundationTests.cs` | `76AD2004AFDDCEBA3C4EA331B9C50A285844638C73FD2DCB36225CEC6106AB19` |

## Commands

| Command | Transcript | Result |
| --- | --- | --- |
| `dotnet build src/CanDoItAll.Git/CanDoItAll.Git.csproj --no-restore` | `proof/SB01/transcripts/git-wrapper-build.txt` | Passed, 0 warnings, 0 errors. |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter ProcessTemplateGitFoundationTests --no-restore -p:BuildProjectReferences=false` | `proof/SB01/transcripts/wrapper-focused-tests.txt` | Passed, 19 tests. |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter ProcessTemplateGitFoundationTests` | `proof/SB01/transcripts/failing-first-wrapper-tests.txt` | Blocked by existing `CanDoItAll.Web (10824)` file locks before meaningful test result. |

## Semantic Adequacy

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-wrapper-tests.txt` records the initial broad test command failing because existing web build outputs were locked.
- Semantic positive proof transcript: `bundle://proof/SB01/transcripts/wrapper-focused-tests.txt` passed the wrapper-focused test fixture.
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` records no stubs in the changed wrapper/test scope.
- Shallow-pass trap: code compiles through `CanDoItAll.Git` directly and focused tests execute the wrapper behavior, not only template tests in the same fixture.
- Semantic positive proof: tests assert exact command argument order for status with branch, name-only diff, unstage, add, commit sanitization, and typed `show`.
- Adversarial negative proof: tests reject `.git`, `.git/config`, `.Git/config`, option-like branch names, and option-like revisions.
- Anti-stub audit: `proof/SB01/anti-stub-audit.txt` contains no matches for TODO, placeholders, default returns, or `NotImplementedException` in the changed wrapper/test scope.
- Raw-note literal closure: `proof/SB01/source-assertions.md` maps the architect notes to the changed source.

## Closure Decision

SB01 is closed. `CanDoItAll.Git` now owns reusable command-spec construction and typed validation for the local operations required by SB02.
