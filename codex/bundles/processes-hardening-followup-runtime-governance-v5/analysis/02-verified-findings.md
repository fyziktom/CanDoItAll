# Verified Findings

## VF01 - The correct branch appears to be `processes-hardening`, not `process-hardening`

The GitHub connector did not find `process-hardening`, but it did find `processes-hardening`. The observed head is `474708e7a09d85a90d9541946e1e0e3dd964ec18` with message `phase4`.

## VF02 - Step operation contract exists, but persistence is still text-driven

`ProcessStepOperation`, `ProcessStepTargetScope`, and `ProcessStepOperationContract` are defined in `ProcessRunAutomationDispatchService.ExecutionMetadata.cs`. However, `ResolveProcessStepOperationContract` derives the contract from step notes, contract summaries, work brief text, expected artifacts, and keyword parsing. Explicit contract detection depends on text containing terms like `operation contract`, `allowed operations`, or `target scope`.

Risk:
- Process authors must know magic phrases.
- Imports/templates can omit contracts and silently fall back to heuristic classification.
- Non-English or business-domain wording may misclassify.

Required improvement:
- Add persisted typed fields on process step definitions and editor models.
- Use text parsing only as migration/backward-compatible fallback.

## VF03 - Tool policy enforces product mutation, but not full operation authorization

`DefaultAgentToolInvocationPolicy` now denies product mutations when `ProcessAllowsProductMutation` is false. It also blocks managed output product writes. This is a real improvement.

Remaining risk:
- `agentProcessStepAllowedOperations` and `agentProcessStepTargetScope` are emitted but the policy mainly uses the product-mutation boolean.
- Validation, runtime launch, browser proof, external action, process definition mutation tools, and skill scripts need operation-specific checks.
- A non-mutating analysis step should not be able to run runtime launch or heavy validation merely because it is not a product mutation.

Required improvement:
- Tool policy should evaluate operation class against allowed operations.
- Add policy decisions for `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, `ExecuteExternalAction`, `RecoverArtifactsOnly`, and process-definition mutations.

## VF04 - Prompt-grounded alias auto-promotion is improved, but grounding is still text-scraped

`ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` now chooses read-only aliases when process boundary metadata disallows mutation. This fixes a previous class of accidental write grants.

Remaining risk:
- `ResolveExternalTargetAliases` still harvests paths from trigger reason, project-structure grounding, artifact inspection, work brief, expected artifacts, and artifact input summaries/provenance/review text.
- Old or sibling paths can become trusted context if they appear in upstream artifact descriptions.

Required improvement:
- Introduce a typed `GroundedTargetAliasLedger`.
- Store source kind, confidence, trust level, and intended use.
- Only project structure target nodes and explicit run launch context should grant write access.

## VF05 - Upstream materialization reactivation is fixed for the uncommitted artifact visibility issue

`RecordArtifactAsync` now passes the materialized artifact into `ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync`, and the method adds it to the artifact list when DB query cannot see it yet.

Remaining risk:
- The reactivation marker is based on text in `BlockedReason` using `ProcessRuntimeProgressionPlanner.IsMissingUpstreamArtifactBlock`.
- Block reasons can change wording and break reactivation.
- The materialization request/resolution lifecycle is not represented as a typed state machine.

Required improvement:
- Store typed block reason code and materialization request state.
- Reactivate by durable materialization request records, not by string markers.

## VF06 - Artifact content validation is stronger but still file-system-bound

`WorkspaceProcessArtifactContentReader` reads managed storage paths by resolving them under workspace root. It validates content bytes for JSON/Markdown/YAML/image cases.

Remaining risk:
- This assumes managed storage is accessible as a workspace file.
- If storage placement later writes to storage drivers, IPFS, object storage, or another non-workspace backend, valid artifacts may fail validation.
- Binary/document formats are not deeply validated beyond image signatures.

Required improvement:
- Introduce an artifact content reader abstraction backed by the storage placement service or storage driver registry.
- Use metadata to resolve storage location instead of assuming workspace-root paths.

## VF07 - Artifact lineage is present but external reference dedupe remains fragile

Artifact records now include `ProjectionLineageJson`, and projection code builds lineage for recovery artifacts. This is a major improvement.

Remaining risk:
- `RecordArtifactAsync` still bounds `ExternalReferenceKey` to 200 characters.
- Deduplication still checks the bounded key.
- Long keys can collide after truncation/hash suffixing or lose semantic details needed by old fallback logic.
- Projection lineage should become the primary dedupe key.

Required improvement:
- Add a stable projection identity hash from typed lineage.
- Use DB uniqueness on `(ProcessRunId, StepRunId, ProjectionIdentityHash)` where appropriate.
- Keep `ExternalReferenceKey` as display/debug compatibility only.

## VF08 - Workflow and subprocess artifact mapping remains heuristic

`ProcessWorkflowRunCoordinator` maps workflow artifacts to process artifacts by workflow artifact kind and name/summary matching. Subprocess parent projection still resolves source artifacts by kind/title/trust.

Risk:
- Wrong workflow artifact can satisfy a process expectation.
- Multiple artifacts of the same kind can bind incorrectly.
- Parent subprocess can bind a child artifact that is only loosely related.

Required improvement:
- Add explicit output mapping metadata for workflow and subprocess assignments.
- Validate artifact expectation mapping at definition publish/start.
- Add red-team tests for same-kind wrong-title/sibling artifacts.

## VF09 - Strict lint mode exists but default behavior can remain advisory

`ProcessDefinitionPublishRequest` has `LintMode = Advisory`. Run-start also uses the request lint mode. This means strong warnings may not block unless callers explicitly choose strict mode.

Risk:
- Unsafe definitions can still be published/started from UI or API defaults.
- Operators may miss warnings unless UI makes them prominent.

Required improvement:
- Define a lint enforcement policy based on process criticality, autonomy, operating mode, and executor type.
- Strict mode should be default for autonomous/agent-executed governed processes or at least for production operating mode.

## VF10 - Runtime invariant auditing is still missing as a final safety net

Tool policy and finalizer validation are stronger, but there is no obvious post-step invariant audit that scans actual recorded tool receipts, artifact lineage, and changed paths to detect boundary violations after the fact.

Risk:
- If a tool path bypasses policy or a script mutates outside the expected boundary, the process may proceed without flagging a governance violation.
- This is especially relevant because the process core must be generic and cannot rely on every tool being perfectly classified.

Required improvement:
- Add a runtime invariant audit after execution/finalization.
- Persist violation records and block/escalate when non-mutating steps mutate product targets or when required evidence cannot be traced.
