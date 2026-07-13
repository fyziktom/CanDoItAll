# SB02 Proof Manifest

## Subbundle

- Subbundle: `02-process-contract-and-template-repair`
- Status: `Completed`
- Owned requirement: repair process template and step-brief contracts so role separation and subprocess boundaries are enforceable.

## Changed Files And Hashes

| File | SHA-256 |
| --- | --- |
| `repo://Templates/Processes/processes/software-delivery/definition.json` | `4AA1A1AA454BEB92441E86407F4160C1E2B7E913C35EE98399FAE64EE9B60FA0` |
| `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json` | `808D0107BE6476EB0C79AAA16F1530E473C497FF493984DD1A2BE2A927F3590C` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub.txt`
- Source assertion: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`

## Closure

- Failing-first: `bundle://proof/SB02/transcripts/failing-first.txt` records the pre-repair template contract mismatch.
- Semantic positive proof: `bundle://proof/SB02/transcripts/passing.txt` records focused template contract tests passing.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub.txt`.
