# SB05 Proof Manifest

## Status

Completed.

## Goal

Refactor project-structure output grounding and final external delivery proof into a dedicated generic service with typed grounding, stale-reference inspection, and adversarial path validation.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs | New typed service for external target grounding, alias normalization, stale-reference inspection, and prompt redaction. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs | Uses typed grounded target results for final-delivery prompt rules. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs | Keeps existing helper surface as wrappers over the shared service. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs | Delegates alias extraction, pruning, and normalization to the shared service for invocation metadata. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | Delegates stale/out-of-scope external target reference inspection to the shared service. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs | Delegates stale path redaction to the shared service. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/README.md | Updates the Processes architecture map with the service boundary. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ProcessExternalTargetGroundingServiceTests.cs | Adds typed grounding and adversarial escaped/prohibited path tests. | bundle://proof/SB05/transcripts/changed-file-hashes.txt |

## Failing-first Or Adversarial Proof

- bundle://proof/SB05/transcripts/failing-first.txt records adversarial tests proving prohibited project-structure targets and escaped sibling paths do not satisfy current-run final delivery semantics.

## Passing Proof

- bundle://proof/SB05/transcripts/passing.txt records 43 passing targeted integration tests across the new service, prompt final-delivery proof, metadata, recovery redaction, and existing compatibility helpers.

## Source Assertions

- bundle://proof/SB05/transcripts/source-assertions.txt records the dedicated service, typed result records, dispatch prompt/metadata/validation consumers, and adversarial tests.

## Anti-stub Audit

- bundle://proof/SB05/transcripts/anti-stub-audit.txt records no TODO, pending, stub, or `NotImplementedException` markers in the SB05 changed runtime, test, and README files.

## Changed-file Hashes

- SHA-256 `FBCE1742CE3826CDC2715AD715C265A040E280F5523B3647A7B5D8BE2DE55A4B` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs
- SHA-256 `E39EBE7C9E22687E12A5D2D90C548E565E5744E7CA1553625A7B7E6AE57EC2BD` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs
- SHA-256 `7040F9872138EE7A32D2EAFA639D61300D9D3A672EF4E8D5726EF3EE8DC4B30E` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs
- SHA-256 `27E39F110A8B34F9BE04FAC931D1A24FF68A98A181B057D7AC7874764AF5E271` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- SHA-256 `BE7C16BF1C47044A2B552A121933D8BCB493B72BEDA08F5E1649F5964BFB0D75` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs
- SHA-256 `4EC244B92BF5FBE0D8D96B9BEC5E44A7116090E1565C90D079A52A9FBD57C8F6` repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs
- SHA-256 `8FC6844E4DCCCB2B943A12BD0CF21BCC879B60A8412AE904D05343406AD36741` repo://src/CanDoItAll.Modules.Processes/README.md
- SHA-256 `E26756BE9EEB86539D2CA38AEA3FA116D3BD523298234CE74EC10E520FEDC74C` repo://tests/CanDoItAll.Tests.Integration/ProcessExternalTargetGroundingServiceTests.cs
- bundle://proof/SB05/transcripts/changed-file-hashes.txt records the command transcript for these hashes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Typed external target grounding result | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs via `ResolveProjectStructureGroundingTarget`; source proof bundle://proof/SB05/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs; prompt proof bundle://proof/SB05/transcripts/passing.txt | Created from current-run project-structure grounding, skips prohibited/non-product candidates, and feeds final delivery/scaffold prompt rules; hash proof bundle://proof/SB05/transcripts/changed-file-hashes.txt | Prohibited targets do not return `HasTarget`; adversarial proof bundle://proof/SB05/transcripts/failing-first.txt |
| External target alias ledger normalization | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs via alias extraction/pruning; source proof bundle://proof/SB05/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs and metadata tests; passing proof bundle://proof/SB05/transcripts/passing.txt | Normalizes absolute and `external-target/...` aliases, collapses traversal segments, prunes ancestor/prefix aliases, and supplies invocation metadata | Escaped sibling aliases are normalized out of the allowed root and rejected; adversarial proof bundle://proof/SB05/transcripts/failing-first.txt |
| Stale external target reference inspection/redaction | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs via `InspectReferences` and `RedactUnallowedReferencesForPrompt`; source proof bundle://proof/SB05/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs; passing proof bundle://proof/SB05/transcripts/passing.txt | Reads current allowed aliases from invocation metadata, classifies stale/out-of-scope references, and redacts stale paths before retry prompts | Escaped absolute sibling paths are blocked/redacted without leaking the stale path; adversarial proof bundle://proof/SB05/transcripts/failing-first.txt |

## Closure

- SB05-INV-001 is satisfied by repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs and bundle://proof/SB05/transcripts/passing.txt.
- SB05-INV-002 is satisfied by bundle://proof/SB05/transcripts/failing-first.txt.
- SB05-INV-003 is satisfied by repo://src/CanDoItAll.Modules.Processes/README.md and bundle://proof/SB05/transcripts/source-assertions.txt.
