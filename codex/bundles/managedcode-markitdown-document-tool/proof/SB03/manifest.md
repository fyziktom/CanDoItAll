# SB03 Proof Manifest

## Scope

- Automated validation.
- 5032 restart.
- Project-structure floating agent chat validation.

## Changed File Hashes

- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceArtifactToolServiceTests.cs` SHA-256 `B15BB2CF67FEC8675A0573901B69ED196E014A7BE2B8C561DDB7C66A095E0220`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ManagedCodeMarkItDownDocumentMarkdownConverterTests.cs` SHA-256 `ADB3689F09260F95F96BAF9FDED15B91182B0AC37119ED970894FF9082B67E74`

## Semantic Contract

- `bundle://proof/SB03/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB03/transcripts/passing-live-conversion.log`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.log`
- Failing-first: N/A process - this subbundle validates the already-implemented live path; the separate approval-continuation blocker is recorded as a follow-up, not as document conversion behavior.
- Screenshot proof: captured in the SB03 browser evidence set and summarized in the execution report.

## Result

- Focused tests passed.
- Live conversion and extraction passed.
- Final node creation is blocked by an existing approval continuation issue.
