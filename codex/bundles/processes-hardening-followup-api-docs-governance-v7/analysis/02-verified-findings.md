# Verified findings

## Finding 1: Operation contracts are now persisted, but the contract model still needs normalization semantics

`ProcessStepDefinition` now has `AllowedOperations` and `OperationTargetScope`. EF stores allowed operations as JSON and target scope as an enum string. The UI exposes both fields.

Remaining gap:
- `ProcessStepOperationContractState.NormalizeAllowedOperations` currently only deduplicates and sorts.
- Runtime later adds implicit operations from target scope, but editor/API/import/export may not show or enforce those implications consistently.
- A definition can still have partial/inconsistent state unless every save/import/API path validates it.

Required fix:
- Promote operation-contract normalization to a single reusable service.
- Validate and normalize before save/import/publish/run-start.
- Emit deterministic lint errors for partial or contradictory contracts.

## Finding 2: Tool policy is operation-aware, but API/tool schema drift is still a correctness risk

Tool policy now uses allowed operations for validation, runtime launch, browser proof, product mutation, artifact record, process step transition, process definition tools, image generation, and skill scripts.

Remaining gap:
- Need verify all `processes_*` tool input models carry `AllowedOperations`, `OperationTargetScope`, contract mode, workflow output mapping, subprocess output mapping, block reason code, and recovery options where applicable.
- Need verify public API/docs and skill docs explain these fields.
- Need tests that tool/API round-trips preserve these fields.

Required fix:
- Add an explicit API schema parity test.
- Update process API/tool docs and related skill(s).
- Add examples for software and non-software process definitions.

## Finding 3: Grounding ledger exists, but policy still primarily consumes alias lists

Dispatch emits `agentProcessGroundedTargetAliasLedger`. However policy still primarily uses `AllowedExternalTargetAliases` and `ReadOnlyExternalTargetAliases`.

Remaining gap:
- Alias authority is still split across three structures: ledger, writable list, read-only list.
- Runtime may still allow or block based on derived string lists rather than trusted source records.
- Same alias can potentially appear in both read-only and allowed lists unless explicitly reconciled.

Required fix:
- Make the ledger authoritative.
- Derive read/write lists from ledger only after overlap resolution.
- Add invariant: a normalized alias has exactly one effective authority per run.

## Finding 4: Workflow/subprocess output mapping exists, but enforcement is incomplete

`ProcessArtifactExpectation` now has:
- `WorkflowOutputId`
- `WorkflowOutputName`
- `WorkflowOutputKind`
- `SubprocessChildArtifactExpectationId`

Remaining gap:
- Linter still mainly checks that workflow steps have required artifacts and validation text.
- Subprocess linter still mainly warns that parent required artifacts depend on child projection.
- Need strict mapping for workflow/subprocess steps when required process artifacts are expected.

Required fix:
- Add strict lint rules and runtime validation for explicit mapping.
- Fail workflow/subprocess completion when mapping is missing or ambiguous.
- Add import/export/API docs for mapping fields.

## Finding 5: Finalizer-grade artifact validation and manual/API transition validation are not unified

`TransitionStepAsync` still performs a simpler completion artifact check based on kind, sensitivity, trust, expectation id/title. It does not appear to use the same finalizer-grade validation for content, producer mode, lineage, placeholder detection, or current-run binding.

Remaining gap:
- Human/API/manual completion could complete a step with weak artifact records that finalizer would reject.
- This can produce false process progress.

Required fix:
- Extract finalizer-grade validation into a process artifact validation service.
- Use it from both automation finalizer and manual/API transition paths.
- Allow explicit override only with a typed human approval/escalation record and audit event.

## Finding 6: Typed block state exists but cause capture is still reason-text inferred

`ProcessStepRunBlockState` applies `BlockReasonCode` and recovery options, but it infers the code from a free-text reason.

Remaining gap:
- Missing own output artifact can be misclassified as missing upstream artifact.
- Policy denied path, tool unavailable, validation failed, runtime invariant violation, and no-progress can be confused by text.
- Transition APIs should carry typed block code and recovery options.

Required fix:
- Extend `ProcessStepTransitionRequest` and relevant tool/API models with optional typed block metadata.
- Make automation finalizer pass the typed cause directly.
- Keep reason inference only as fallback for old data.

## Finding 7: Script side-effect policy is stronger but still not enough for durable governance

Policy inspects script content and detects common PowerShell/Python write signals.

Remaining gap:
- Regex inspection cannot reliably detect nested scripts, encoded commands, .NET file APIs, shell redirection, package scripts, or tool side effects.
- A post-execution artifact/diff audit is still needed.

Required fix:
- Require script side-effect manifests for governed process steps.
- Capture before/after fingerprints for product and managed artifact roots.
- Block or flag any undeclared mutation.

## Finding 8: Skills and docs appear behind the runtime

Phase6 changed runtime, tests, migration, and Blazor templates. No clear related process skill/doc update was visible in the reviewed changed-file set beyond bundle documentation and template JSON/docs.

Remaining gap:
- Codex and agents may still rely on older prose-only process knowledge.
- Process API users may not know how to supply typed operations, target scopes, workflow/subprocess output mappings, block reason codes, recovery options, and contract mode.

Required fix:
- Add/update a dedicated `processes-runtime-governance` skill or update the relevant existing process skill.
- Update docs and examples.
- Add a documentation parity test/audit.
