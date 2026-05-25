# Verified Findings

## VF01 — Upstream materialization reactivation can miss the artifact just recorded

`RecordArtifactAsync` adds a `ProcessArtifactRecord`, then calls `ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync`, and only after that saves changes. The reactivation method queries `ProcessArtifactRecord` through EF against the database, so it can miss the newly added unsaved artifact.

Impact:

- downstream step can remain `Blocked`
- materialization can appear complete while no dependent step resumes
- process stalls without a clear runtime reason

Required fix:

- save artifact first inside transaction before reactivation, or
- pass the new artifact into the reactivation calculation, or
- query tracked local changes plus persisted records

## VF02 — Recovery lineage can be destroyed by external reference key truncation

`RecordArtifactAsync` bounds `ExternalReferenceKey` to 200 characters. `ApplyArtifactProjectionLineage` builds long keys containing recovery execution run id, recovered-for execution run id, projected execution run id, optional rework packet id, and original external reference key.

Impact:

- dedupe keys can collide
- recovery lineage GUIDs can be truncated
- producer detection and current-run validation can become unreliable
- later replay/debugging loses lineage

Required fix:

- stop storing full lineage in bounded external reference key
- introduce typed provenance fields/table or JSON payload
- keep external reference key short and stable
- use a separate hash for uniqueness

## VF03 — Script/run tools can bypass non-mutating step boundaries

Tool policy now blocks direct product mutation tools when product mutation is disallowed. But script/run tools can mutate files inside helper scripts without the tool policy seeing file-level side effects.

Examples:

- `workspace_pwsh_run_script` with a managed script that writes product files
- `workspace_python_run_file` that edits target files
- `workspace_dotnet_run` or external scripts generating files as side effects

Impact:

- architecture/review/analysis steps can still implement indirectly
- process boundary metadata gives false confidence
- source drift can happen without direct write-file calls

Required fix:

- classify script/run tools by step operations
- require script content inspection for non-mutating steps
- restrict script tools in analysis/review steps to read-only or artifact-only helpers
- detect product-target strings inside helper scripts before execution
- record script side-effect manifests when allowed

## VF04 — External target grounding is still text-derived and can be poisoned

`ResolveExternalTargetAliases` collects aliases from trigger reason, project-structure grounding, artifact inspection grounding, work brief fields, expected artifact summaries, and upstream artifact titles/review/provenance text.

Impact:

- stale upstream artifact text can ground a wrong external-target path
- sibling path references can become allowed read/write targets
- provenance and review summaries become authority sources accidentally
- process may inspect or mutate the wrong target

Required fix:

- use typed `ProcessTargetGroundingRecord`
- classify grounding source as `ProjectStructureCurrentRun`, `RunLaunchPlan`, `UpstreamArtifactReference`, `ToolReceipt`, `TextMention`
- allow writable targets only from trusted current-run source kinds
- keep text mentions read-only unless promoted by an explicit target record

## VF05 — Artifact validation is not truly storage-backed

`HasValidJsonArtifactContent` validates JSON only when `ManagedStoragePath` is an absolute path that exists or when inline JSON exists in review/provenance summary. Most process-managed storage paths are relative managed paths, so malformed JSON can pass validation.

Impact:

- format validation can be false-positive
- finalizer can accept invalid structured artifacts
- process can proceed on broken JSON payloads

Required fix:

- resolve managed storage path through the storage provider
- read artifact bytes through `storagePlacementService` or an artifact content reader
- validate JSON/YAML/Markdown/image/file-size constraints from actual stored content
- produce durable validation diagnostics when content is unreadable

## VF06 — Workflow artifact mapping is still heuristic

Workflow artifact projection maps workflow artifacts to expectations by artifact kind, title, and summary. That can mismatch process expectations when multiple artifacts of the same kind exist or when workflow naming differs.

Impact:

- wrong workflow artifact can satisfy a process expectation
- required artifact can be missed even though workflow output exists
- process finalizer becomes brittle around naming

Required fix:

- add explicit workflow-output-to-process-artifact mapping
- require mapping for workflow-backed steps with more than one required artifact
- include node id, artifact id, workflow output key, expected process artifact id
- fail lint/readiness when mappings are ambiguous

## VF07 — Subprocess parent artifact projection is still kind/title heuristic

Subprocess source artifact resolution uses kind, sensitivity, trust, and title ordering. It does not require an explicit mapping from child artifact expectation to parent artifact expectation.

Impact:

- parent step can receive a wrong child artifact of same kind
- required parent artifact can be falsely satisfied
- child process changes can break parent projection silently

Required fix:

- introduce subprocess output mapping
- map child expectation id -> parent expectation id
- validate child run id/version lineage
- block or require review when mapping is ambiguous

## VF08 — Disposition routing can still mask missing own artifacts

`ResolveArtifactContractDispositionBranchOutcome` routes unsatisfied artifact validation to a negative branch unless missing upstream inputs exist or a narrow hard-blocking condition matches. `IsHardBlockingArtifactValidationFailure` only checks `Missing` plus diagnostic text containing `upstream`.

Impact:

- an artifact-producing step with missing own required artifacts can complete onto a negative branch
- branch routing can hide agent failure to produce its own process artifact
- downstream repair can start from an incomplete ledger

Required fix:

- classify failures by ownership: own artifact production vs upstream evidence vs review disposition
- own required artifacts must block/recover, not branch-route, unless explicit policy allows it
- disposition routing should apply mainly to review/approval/QA decisions, not artifact production failures

## VF09 — Operation contracts are text-parsed rather than first-class fields

`TryResolveExplicitOperationContract` searches notes/contracts for phrases such as `operation contract`, `allowed operations`, and target scope tokens.

Impact:

- process designers must know magic text phrases
- import/export cannot reliably preserve typed semantics
- UI cannot provide clear validation or selection
- linter can only infer intent

Required fix:

- add typed fields to `ProcessStepDefinition` and editor/import/export models
- provide UI selectors for allowed operations and target scope
- use text parser only as migration/backfill helper
- make typed fields the source of truth

## VF10 — No-progress retry compression is not durable enough

Tool-policy invocation counts and no-progress retry logic can reset with new policy instances or runtime restarts. Some retry classifications also rely on response text.

Impact:

- repeated bad behavior can recur after restart
- no-progress history is hard to audit
- retries can be consumed without causal change

Required fix:

- persist `ProcessStepNoProgressFingerprint` journal or table
- record tool signature, failed validation, artifact failure, mutation generation, and execution run id
- stop retry when the same fingerprint repeats without new evidence or mutation
- expose actionable recovery reason in run health

## VF11 — Lint gates are not strict enough by default

`ProcessDefinitionPublishRequest` defaults `LintMode` to `Advisory`. UI displays lint issues, but ordinary publish/start paths can still allow warnings unless callers explicitly request strict lint.

Impact:

- risky definitions can reach runtime
- process safety depends on operator behavior
- high-criticality processes are not automatically protected

Required fix:

- derive strictness from process criticality/autonomy
- require strict lint for high-criticality or autonomous processes
- add UI control and clear publish/start blockers
- include full lint issue list, not only top four, for review
