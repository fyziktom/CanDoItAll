# Target Solution

## Runtime Flow

1. Resolve launch-variable templates with a bounded resolver before agent dispatch and before any rework packet is emitted.
2. Preflight tool-critical plans using resolved values, exact tool names, argument shape, target paths, scopes, and side-effect manifests.
3. Convert finalizer `Completed` into a staged result, not an accepted process completion.
4. Evaluate completion gates through an aggregate evaluator.
5. Prioritize diagnostics deterministically while preserving all gate issues.
6. Classify recovery using retry safety, idempotency, policy, retry budget, and diagnostic fingerprint.
7. For safe/idempotent gate failures, emit diagnostic-specific current-step rework.
8. Accept/promote produced artifact slots only after completion gates pass.
9. Propagate child root cause, accepted slots, and no-go outcomes through the subprocess bridge.
10. Escalate only when retry is unsafe, denied, exhausted, or semantically blocked.

## Proposed Services

- `ILaunchVariableTemplateResolver`
- `IProcessCompletionGateEvaluator`
- `IGroundedEvidenceGate`
- `IManagedArtifactGate`
- `IProductMutationReceiptGate`
- `IRequiredToolReceiptGate`
- `IProductPathGate`
- `IProductReadbackGate`
- `IRequiredProductStateGate`
- `ICompletedWithoutDeclaredBlockerGate`
- `IRequiredToolReceiptMatcher`
- `IProcessRecoveryClassifier`
- `IProcessStepRecoveryInstructionBuilder`
- `ISubprocessRunStateResolver`
- `ISubprocessArtifactBridge`
- `IProcessStepToolPlanGuard`
- `IProcessStepToolPlanExecutor`

## Minimality Rule

Start with extracted services inside existing owning projects. Move contracts into `CanDoItAll.Processes.Contracts` only when they must be shared across runtime/application/templates/modules. Do not create new projects for this repair unless dependency direction forces it.

## Success Criteria

- The 5032 calculator incident routes to bounded rework with exact missing helper receipt and solution readback diagnostics.
- All affected templates have typed hard gates or explicit audit exceptions.
- Runtime and template tests reject shallow success based on file existence, prose-only instructions, or generic completed status.
