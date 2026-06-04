# SB01 Proof Manifest

## Scope

- Subbundle: `SB01 project rename and reference repair`
- Raw notes closed: `N001`, `N002`, `N003`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

- Hash inventory: `bundle://proof/SB01/changed-file-hashes.txt`
- Representative SHA-256: `dd08b321cd24ff9cfdb2d5195201daf8803979bc65fa2733e40a6f9ed6600f74` for `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`.

## Source Assertions

- Source assertions: `bundle://proof/SB01/source-assertions.md`
- Project identity source: `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- Solution source: `repo://CanDoItAll.slnx`
- Web consumer source: `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- Test consumer source: `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## Command Transcripts

- Passing project build: `bundle://proof/SB01/transcripts/renamed-project-build.txt`
- Passing component tests: `bundle://proof/SB01/transcripts/component-tests.txt`
- Adversarial negative proof: `bundle://proof/SB01/transcripts/stale-reference-search.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Failing-first proof: N/A - process/non-production rename with no behavior-specific failing-first test; stale-reference search is the adversarial negative proof.

## Semantic Invariant Coverage

- `SB01-PROJECT-IDENTITY`: build and stale-reference transcripts prove the renamed project identity.
- `SB01-CONSUMER-REFERENCES`: component-test and stale-reference transcripts prove direct consumer repair.
- `SB01-SIBLING-BOUNDARY`: source assertions and anti-stub audit prove package references and sibling pointers were not broadly renamed.

## Browser Proof

- N/A. SB01 changes project identity and compile-time references only; no rendered UI behavior was intentionally changed.
