# SB05 Semantic Invariants

## Invariants

- Invariant ID: SB05-INV-001
- Source raw note: RN05 - Refactor output grounding and final external delivery proof into a dedicated generic service.
- Expected behavior: Final-delivery prompt rules, invocation metadata, artifact validation, and recovery redaction consume one typed external-target grounding and reference-inspection service.
- Disallowed shallow implementation: Prompt-only wording, docs-only behavior, string-only wrappers without typed results, path traversal accepted as a descendant, stale path leakage in recovery prompts, or hardcoded Blazor/Tetris/project/run/user paths.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt proves the old prompt-local out-parameter grounding path is absent and adversarial prohibited/escaped path cases are rejected.
- Passing test: bundle://proof/SB05/transcripts/passing.txt proves the new service, final-delivery prompt behavior, invocation metadata, recovery redaction, and compatibility helper tests pass.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs; repo://src/CanDoItAll.Modules.Processes/README.md; repo://tests/CanDoItAll.Tests.Integration/ProcessExternalTargetGroundingServiceTests.cs.
- Production assertions: The runtime now uses `ProcessExternalTargetGroundingService` typed records for grounded targets, scaffold hints, alias pruning, stale-reference inspection, and prompt redaction; no project-specific runtime constants were introduced.
- Red-team negative case: A prohibited project-structure path, a sibling path reached through traversal, or a stale sibling path in retry text cannot become current-run final delivery proof.
- Downstream dependency check: SB06, SB12, SB16, and SB18 can rely on centralized final-delivery grounding semantics and the recorded adversarial path proof.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Typed external target grounding result | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs `ResolveProjectStructureGroundingTarget`; source proof bundle://proof/SB05/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs final-delivery prompt rules | bundle://proof/SB05/transcripts/passing.txt proves grounded target and scaffold contract behavior through service and prompt tests | bundle://proof/SB05/transcripts/failing-first.txt proves prohibited targets do not return `HasTarget` |
| External target alias ledger normalization | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs alias extraction and pruning | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs invocation metadata builder | bundle://proof/SB05/transcripts/passing.txt proves metadata and compatibility helpers still accept valid grounded aliases | bundle://proof/SB05/transcripts/failing-first.txt proves traversal sibling aliases are rejected after normalization |
| Stale external target reference inspection and redaction | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs `InspectReferences` and `RedactUnallowedReferencesForPrompt` | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs | bundle://proof/SB05/transcripts/passing.txt proves out-of-scope detection and stale-path recovery redaction remain green | bundle://proof/SB05/transcripts/failing-first.txt proves escaped stale sibling paths are blocked or redacted without leaking the stale path |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB05/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB05/transcripts/passing.txt.
- Source assertions: bundle://proof/SB05/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB05/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB05/transcripts/changed-file-hashes.txt.
