# Verified Findings

## VF01: Prompt-grounded alias read-only autopromotion can conflict with writable aliases

`ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` now sends all prompt-discovered aliases to `ReadOnlyExternalTargetAliases` whenever process boundary metadata exists. If metadata already contains the same alias in `AllowedExternalTargetAliases`, the alias can become both writable and read-only. The policy checks read-only mutation later and can deny a valid product mutation.

Impact:
- legitimate product mutation steps can get blocked after prompt grounding;
- behavior depends on whether the prompt repeats an already allowed alias.

Required fix:
- prevent duplicates across writable and read-only alias sets;
- if an alias is already writable from a trusted ledger source, prompt grounding must not add it as read-only;
- if prompt grounding discovers a narrower alias inside a writable root, keep it covered by writable root;
- if prompt grounding discovers a sibling or parent outside writable root, add it as read-only or deny access, never writable.

## VF02: Projection identity hash exists but may not be materialized consistently

`ProcessArtifactRecord` now has `ProjectionLineageJson` and `ProjectionIdentityHash`, and EF has a unique index on `(ProcessRunId, ProjectionIdentityHash)`. `ProcessArtifactProjectionLineageJson` computes an identity hash. But the runtime must prove that `RecordArtifactAsync` sets `ProjectionIdentityHash` from normalized lineage before insert/update and uses it for dedupe.

Impact:
- unique index can be inert if the column stays empty;
- dedupe may still rely on bounded `ExternalReferenceKey`;
- recovery artifacts may duplicate under retries.

Required fix:
- `RecordArtifactAsync` must normalize lineage once;
- persist `ProjectionLineageJson` and `ProjectionIdentityHash` from the same normalized object;
- dedupe by `ProjectionIdentityHash` before fallback `ExternalReferenceKey`;
- keep `ExternalReferenceKey` as display/reference only, not identity source.

## VF03: Manual/process API transitions still use weaker artifact checks

`TransitionStepAsync` still validates required artifacts using local `SatisfiesArtifactExpectation`, which only checks kind, sensitivity, trust, expectation id/title. It does not validate stored content, producer mode, lineage, placeholder state, or current-run binding the way the finalizer does.

Impact:
- manual or API transitions can complete a step with malformed or placeholder artifacts;
- finalizer and manual transition semantics diverge.

Required fix:
- unify manual/API completion validation with the same artifact contract validator used by process-owned finalizer;
- exception branch routing must use typed disposition policy;
- keep fast path only for branches explicitly configured to skip own output artifacts.

## VF04: Block reason classification still relies on reason text and can misclassify own missing artifacts as upstream missing artifacts

`ProcessStepRunBlockState.InferBlockReasonCode` checks phrases such as `missing required artifact` and `required artifacts remain missing` under `MissingUpstreamArtifact`. That conflates own-output artifact contract failure with upstream input materialization failure.

Impact:
- runtime can offer wrong recovery options;
- downstream materialization can be requested when the current executor simply failed to write its own artifact;
- process health becomes misleading.

Required fix:
- propagate typed block codes at the point where the block is created;
- do not infer own/upstream ownership from human-readable text if finalizer diagnostics already know `FailureOwnership`;
- keep inference only as a legacy fallback.

## VF05: Script side-effect policy is regex based and can be bypassed

Script inspection checks a bounded text file and uses regexes for common PowerShell/Python write operations. This is useful but not enough. Examples that may bypass or confuse it include .NET static IO APIs, command redirection, nested scripts, encoded commands, shelling to `cmd`, `powershell -EncodedCommand`, package scripts, and generated child scripts.

Impact:
- review/architecture/validation steps could mutate products through scripts;
- allowed validation scripts can become hidden mutation channels.

Required fix:
- require a declarative script side-effect manifest for governed script execution;
- for non-mutating steps, block script execution unless the manifest says no product mutation and policy can verify target paths;
- optionally add post-script artifact/path diff auditing.

## VF06: Trusted target grounding ledger is emitted but policy still primarily consumes alias lists

The process metadata now has a grounded target alias ledger, but the policy decisions shown still work mostly from `AllowedExternalTargetAliases` and `ReadOnlyExternalTargetAliases`. The ledger must become authoritative: source, authority, intended use, confidence, and scope need to be parsed and used by policy.

Impact:
- alias path heuristics such as `output`, `artifact`, `report`, `src`, `app` still decide product vs artifact semantics;
- a product folder named `output` or a report folder named `app` can be misclassified.

Required fix:
- parse ledger records in `WorkspaceExecutionAuditContext`;
- tool policy must use ledger authority/intended use before string heuristics;
- string heuristics are only fallback for legacy metadata.

## VF07: Workflow/subprocess output mapping is still heuristic

Workflow artifacts are matched by artifact kind, title, and summary. Subprocess parent projection still resolves child source artifacts by kind, sensitivity, trust, and title-like matching.

Impact:
- wrong artifact can satisfy the wrong expectation when multiple artifacts share kind/title fragments;
- process parent may complete based on accidental match.

Required fix:
- add explicit mapping definitions from workflow/subprocess output ids or names to process artifact expectations;
- finalizer should block ambiguous mappings instead of guessing.

## VF08: Artifact content validation is filesystem-backed, not storage-service-backed

`WorkspaceProcessArtifactContentReader` resolves managed paths under the workspace root. This is acceptable for current workspace artifacts, but CanDoItAll already has storage/IPFS ambitions. The finalizer should validate through a storage abstraction that knows managed storage placement, not by assuming a filesystem path.

Impact:
- storage-backed artifacts outside workspace can falsely fail;
- IPFS/remote storage integration will be difficult;
- validation policy becomes tied to local workspace shape.

Required fix:
- introduce `IProcessArtifactContentReader` implementation backed by storage placement service or storage driver;
- workspace reader becomes one implementation/fallback.

## VF09: ProcessStepOperationContractState is useful but currently hidden inside definitions/config patterns

Persisted operation state exists, but the runtime still has fallback inference and text parsing. There is not yet a clear policy for legacy/migrated definitions vs new strict definitions.

Impact:
- old definitions can continue to rely on heuristics indefinitely;
- future process templates may be ambiguous without failing lint.

Required fix:
- add a `ContractStrictness` or definition version gate;
- new/edited process versions should require explicit operation contracts for risky step kinds;
- migrated definitions may run in compatibility mode with warnings.

## VF10: Process health/recovery options are not yet an executable recovery router

Typed block code and recovery options exist, but they mostly annotate the step. Runtime needs a router that decides which automated recovery option is allowed, which is pending, and which needs human escalation.

Impact:
- process may still sit blocked with options displayed but no actionable lifecycle;
- manager recovery vs rerun vs human escalation can be inconsistent.

Required fix:
- create `ProcessRecoveryRouter`;
- use typed block reason and diagnostics to enqueue exact recovery action;
- persist recovery lifecycle events and next eligible action.
