# SB04 Git Safety Review

## Result

Accepted.

## Findings

- `CanDoItAll.Git` contains the only new product Git command execution path.
- The wrapper uses `ProcessStartInfo.ArgumentList`; it does not build shell command strings from paths.
- Repository paths are authorized against a typed repository root before use.
- Commit messages are marked sensitive in command specs and masked from sanitized logs.
- `CanDoItAll.Git` has zero references to Process template/runtime types.
- `CanDoItAll.Processes.Templates` has zero references to the Git wrapper; Application will compose template operations with Git later.

## Scan Disposition

`transcripts/ad-hoc-git-invocation-scan.txt` found two existing test-only hygiene checks:

- `tests/CanDoItAll.Tests.Unit/RepositoryNamingHygieneTests.cs`
- `tests/CanDoItAll.Tests.Unit/RepositoryTransientArtifactHygieneTests.cs`

These are not product Git integration paths and are not blockers for SB04. No active product/service Git invocation outside `CanDoItAll.Git` was introduced by this subbundle.

`transcripts/template-projection-source-review.txt` found the expected projection enum values and source-hash metadata. Markdown, Mermaid, compatibility reports, and import envelopes are modeled as projections with source hashes, not as canonical template input.
