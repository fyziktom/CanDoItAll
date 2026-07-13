# SB07 Proof Manifest

## Changed Files

- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.Artifacts.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateCompatibilityHistoryTests.cs`
- `repo://Templates/Processes/processes`
- SHA-256 `B150ABF771E7D713C28C7B0148526FFBE8EE5B5D03924748CD889E78B28C93ED` for `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.Artifacts.cs`
- SHA-256 `B94A38F28E753346AB575B1B7A25598437E9C42C128CB4A75E836F45A43D179C` for `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateCompatibilityHistoryTests.cs`

## Behavior Moved Out Of Adapter

Template and artifact deterministic proof is enforced in the template compatibility scanner rather than adapter prompt text.

## Tests Added Or Updated

- Test name: `ProcessTemplateCompatibilityHistoryTests`
- Test name: `Template_compatibility_strict_scan_rejects_file_only_artifact_acceptance_contract`

## Test Transcript

- Passing transcript: `bundle://proof/SB07/transcripts/passing.txt`
- Adversarial negative proof transcript: `bundle://proof/SB07/transcripts/passing.txt`

## Build Transcript

- Managed build proof: `bundle://proof/SB07/transcripts/passing.txt`

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260709182007-390484e5`
- Dependency result: `cycles: []`

## Source Assertions

- 24 process templates inspected.
- 20 artifact templates inspected.
- Missing semantic acceptance contract count: 0.
- File-only acceptance count: 0.

## Template And Artifact Matrix

| Inventory | Count | Proof |
|---|---:|---|
| Process templates | 24 | `bundle://proof/SB07/transcripts/passing.txt` |
| Templates with branch rules | 21 | `bundle://proof/SB07/transcripts/passing.txt` |
| Templates with subprocess contracts | 2 | `bundle://proof/SB07/transcripts/passing.txt` |
| Templates with typed execution contracts | 3 | `bundle://proof/SB07/transcripts/passing.txt` |
| Artifact templates | 20 | `bundle://proof/SB07/transcripts/passing.txt` |
| Artifact templates allowing file-only acceptance | 0 | `bundle://proof/SB07/transcripts/passing.txt` |

## Partial-Class Policy Proof

- No adapter partial file was added by SB07.

## Domain-Boundary Source Assertion

- Template terms remain in `repo://Templates/Processes` and `repo://src/Processes/CanDoItAll.Processes.Templates`; generic runtime consumes typed contracts.

## Semantic Invariant Contract

- `bundle://proof/SB07/semantic-invariants.md`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/passing.txt`

## Risks Left Open

- 5032-style live browser/process validation was not exercised; local closure uses strict scanner, focused unit tests, solution build, source audit, and CodeAnalytics.
