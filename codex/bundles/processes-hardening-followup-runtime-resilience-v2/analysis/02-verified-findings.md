# Verified Findings

## F01 - Step boundary classifier can still misclassify artifact-only Work steps as product mutation

`ResolveProcessStepExecutionBoundary` uses broad text heuristics. `LooksLikeProductMutationBoundary` treats tokens such as `create`, `generate`, `build`, and `implementation` as mutation signals. For `ProcessStepKind.Work`, it can return product mutation even when the step only creates an architecture, plan, report, decision, or analysis artifact.

Risk:

- Architecture or planning agents may receive mutating tool profiles.
- The Blazor failure mode can return under a slightly different step title.
- Business/research processes can be misclassified when they "create" a report or plan.

Required fix:

- Add explicit operation contract.
- Distinguish `WriteManagedProcessArtifact` from `MutateProductTarget`.
- Make heuristic inference lower priority than explicit step metadata and artifact modes.

## F02 - Metadata can still be undermined by prompt-grounded external alias auto-promotion

`ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` can merge prompt aliases into allowed aliases when workspace access allows write files. That is dangerous for governed process runs unless it respects process boundary metadata and read-only aliases.

Risk:

- A prompt containing a product root alias can become writable even when `BuildProcessInvocationMetadataJson` intended read-only access.
- Architecture/review steps may still mutate external product roots if workspace profile permits writes for artifact production.

Required fix:

- Make prompt alias grounding process-boundary-aware.
- Never auto-promote read-only aliases to allowed aliases in trusted governed process runs.
- Add tests that architecture/review steps cannot mutate external target or managed product output paths.

## F03 - Tool policy appears to test only external-target write denial

The observed test `ToolPolicy_rejects_product_mutation_against_read_only_process_boundary` denies `workspace_write_file` against a read-only `external-target/...` alias. That is useful but incomplete.

Risk:

- Managed output root product mutation may still be possible in analysis/review steps.
- Artifact writes and product writes are both `workspace_write_file`; path semantics must distinguish them.
- A malicious or confused agent can write source-like files under managed output folders.

Required fix:

- Add path classifiers: process artifact path, evidence path, managed product output path, external product target, external artifact destination.
- For non-mutating boundaries, allow managed artifact/evidence writes but deny product-like writes.

## F04 - Manager recovery artifacts can be rejected as stale/wrong-run

Inside `FinalizeStepCompletionAsync`, when missing artifacts trigger manager recovery, the code validates again using the original `ProcessStepCompletionFinalizerContext`. Recovery artifacts projected from the recovery manager execution can contain the recovery execution run id, not the original execution run id.

Risk:

- Honest manager recovery artifacts may be classified as `StaleOrWrongRun`.
- Recovery may produce artifacts but finalizer still blocks.
- The system may loop into unnecessary recovery/blocking behavior.

Required fix:

- Carry recovery execution detail into the post-recovery validation context.
- Add explicit `RecoveredForExecutionRunId` and `RecoveryExecutionRunId` lineage.
- Validate recovery artifacts against recovery lineage, not only original execution run id.

## F05 - Workflow-backed finalization still needs a real workflow artifact adapter

The dispatch candidate now includes expected artifacts for workflow-backed roles, but finalizer validation only sees `ProcessArtifactRecord` rows for the process step. If workflow runtime does not project workflow outputs into process artifacts before finalization, valid workflow completions can block.

Risk:

- Workflow-backed process roles become brittle.
- A workflow can complete successfully but the parent process step blocks because no process artifact record exists.
- Conversely, stale records could satisfy the process if not versioned.

Required fix:

- Add an explicit process-owned workflow artifact projection adapter.
- The adapter must map workflow run outputs to process artifact records before finalizer validation.
- It must record workflow run id, workflow node id/output id, content hash, and artifact expectation id.

## F06 - Subprocess parent artifact projection is better but not versioned enough

Source-less subprocess projection now records a gap instead of a fake parent artifact. Good. But parent artifacts from earlier child runs can still make projection skip work because `SatisfiesArtifactExpectation` does not check source subprocess run id.

Risk:

- Parent step may keep an old projected child artifact after child rerun/recovery.
- Current child run can be completed but parent evidence belongs to a previous child run.
- Parent step may complete on stale projection.

Required fix:

- Parent projected artifacts must carry `SourceSubprocessRunId` or source run metadata.
- Parent finalizer must require current subprocess run id for subprocess-produced artifacts.
- Projection should replace/supersede prior parent projections for the same expectation and different child run.

## F07 - Upstream materialization records requests but lacks a complete unblock lifecycle

The runtime blocks a downstream step and requests rerun/materialization from the upstream step. It also records a fingerprint to avoid duplicate materialization. However, dispatch candidates generally load only `Ready`, `WaitingApproval`, and `InProgress` steps. A downstream `Blocked` step needs a durable unblock path when the upstream artifact appears.

Risk:

- Downstream steps remain blocked even after upstream artifact materialization succeeds.
- Operators may need manual intervention.
- Process automation appears stuck although the missing artifact has been produced.

Required fix:

- Add `MissingUpstreamArtifactMaterializationResolved` event.
- On source step completion/artifact record, find blocked dependents with matching materialization request and transition them to `Ready` or `WaitingApproval` according to original rules.
- Add idempotent unblock tests.

## F08 - Negative disposition routing can hide artifact production failure

`ResolveArtifactContractDispositionBranchOutcome` routes unsatisfied artifacts to repair/no-go/escalation branches when branch outcomes exist, except for a limited hard-block case. That is right for review/approval decisions but dangerous for artifact-production steps.

Risk:

- A step that was supposed to produce an artifact can "complete" on a repair branch instead of blocking.
- A missing required artifact can be hidden as a negative disposition even when no governed review decision was made.
- The next step may consume an incomplete artifact path.

Required fix:

- Only route artifact validation failures to branch outcomes on decision/review/approval/QA disposition steps.
- Artifact production, implementation, planning, and architecture-output steps should block or recover when their own required artifact is missing.
- Branch routing must require the branch to be semantically compatible with the failure type.

## F09 - JSON validation only reads absolute paths or inline JSON, not managed relative paths

`HasValidJsonArtifactContent` parses content only when `ManagedStoragePath` is rooted and exists, or when JSON is inline in summaries. Managed storage paths are usually relative. Therefore many malformed JSON artifacts can pass as long as they have `.json`.

Risk:

- Invalid JSON process artifacts can satisfy required contracts.
- Later steps fail on format assumptions.
- The finalizer gives a false sense of safety.

Required fix:

- Resolve managed relative paths through the storage/workspace root.
- Read actual artifact bytes through a storage abstraction, not direct `File.Exists` on only rooted paths.
- Add tests for malformed relative managed JSON.

## F10 - No-progress compression is too weak when an agent writes repeated bad evidence

`ShouldCompressNoProgressRetry` returns false when the current attempt has any successful evidence or mutation receipt. If the agent repeatedly writes invalid artifacts, wrong-root files, or malformed reports, the retry loop may continue because each attempt wrote "something".

Risk:

- The runtime still burns retries without semantic progress.
- A run can produce multiple bad artifacts and still not compress.
- Recovery diagnostics become noisy.

Required fix:

- Compare failure fingerprints, artifact expectation ids, target paths, content hashes, and required tool gaps across attempts.
- Treat repeated invalid artifacts with no changed satisfied expectation as no-progress.
- Compress to recovery/escalation earlier.

## F11 - Active concurrent execution adoption may observe a non-terminal run

`TryAdoptConcurrentAutomationExecutionAsync` can adopt a blocking active automation run by fetching its details. If the run is still `Preparing` or `Running`, the caller can evaluate incomplete detail as if it were an outcome.

Risk:

- Premature completion/failure interpretation.
- Repeated adoption loops.
- Stale running state creates confusing dispatch behavior.

Required fix:

- Adopt only terminal runs, or observe active runs until terminal/stale with bounded polling.
- Keep active non-terminal runs blocking but do not run completion logic on them.
- Add a test where an active running execution is not finalized.

## F12 - ProcessDefinitionLinter is currently passive and too heuristic

The linter exists but appears as a standalone analyzer. It warns on ambiguous boundary, workflow missing artifacts, subprocess mapping, branch outcomes, and artifact conflicts. That is useful but insufficient if not integrated into publish/start/run readiness.

Risk:

- Bad process definitions can still be launched.
- Warnings are ignored.
- Runtime receives ambiguous contracts and then has to guess.

Required fix:

- Integrate lint into process definition publish/start UI/API.
- Add severity rules and optional strict mode.
- Add machine-readable lint issue ids and auto-fix suggestions.
