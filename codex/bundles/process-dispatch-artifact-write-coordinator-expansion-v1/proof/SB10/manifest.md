# SB10 Critical Manifest

Subbundle: SB10 - Migrate provider-native browser artifact write paths through coordinator
Status: Completed
Owned requirements: RQ-001, RQ-009, RQ-012, RQ-013
Criticality: Critical. Provider-native browser artifacts feed runtime validation and have distinct expected-output and discovered-output modes.

## Critical Invariants

- Expected provider-native output mode remains required-artifact driven and uses `PlanExpectedOutput`: `bundle://proof/SB10/semantic-invariants.md`.
- Discovered provider-native output mode remains standalone-output capable and uses `PlanDiscoveredOutput`: `bundle://proof/SB10/semantic-invariants.md`.
- Both provider-native write paths use `ProcessArtifactProjectionWriteCoordinator` without direct placement/record calls: `bundle://proof/SB10/source-assertions/provider-native-browser-source-scan.txt`.
- Coordinator source scan shows no provider-native browser source discovery, file-copy, or mode-specific planning semantics moved into the coordinator: `bundle://proof/SB10/source-assertions/provider-native-browser-source-scan.txt`.

## Proof

| Evidence | Path |
| --- | --- |
| Failing-first source guard | `bundle://proof/SB10/transcripts/failing-first-provider-native-browser-source-guard.txt` |
| Passing tests and full build | `bundle://proof/SB10/transcripts/provider-native-browser-tests.txt` |
| Source assertions | `bundle://proof/SB10/source-assertions/provider-native-browser-source-scan.txt` |
| Semantic invariants | `bundle://proof/SB10/semantic-invariants.md` |
| Anti-stub audit | `bundle://proof/SB10/source-assertions/anti-stub-audit.txt` |
| Changed-file hashes | `bundle://proof/SB10/source-assertions/changed-file-hashes.txt` |

## Browser And Host Proof

- Browser proof: N/A. SB10 imports provider-native browser artifact files through service/runtime projection tests; no browser UI proof was required.
- Host proof: N/A. No shell launch, file-open, elevation, or desktop integration behavior changed.

## Completed Validator Proof Labels

- Semantic invariant contract: SB10 semantic contract at bundle://proof/SB10/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB10/transcripts/failing-first-provider-native-browser-source-guard.txt
- Passing transcript: bundle://proof/SB10/transcripts/provider-native-browser-tests.txt
- Anti-stub audit transcript: bundle://proof/SB10/transcripts/anti-stub-audit.txt
- Representative SHA-256: 2A0F709DFF4E9C2D75FA2BD3DAFC19E341BA531574B360A57E9DD92CB1DB92DA
