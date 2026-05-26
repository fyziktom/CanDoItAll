# Verified Findings

## F01: Potential compile breaker: missing `ProcessStepRecoveryOption.None`

Reviewed source shows `ProcessRuntimeViewModels.cs` initializes:

```csharp
public ProcessStepRecoveryOption NextRecoveryAction { get; init; } = ProcessStepRecoveryOption.None;
public ProcessStepRecoveryOption RecommendedAction { get; init; } = ProcessStepRecoveryOption.None;
```

But `ProcessDefinitionEnums.cs` currently shows `ProcessStepRecoveryOption` starting with:

```csharp
WaitForArtifactMaterialization,
RecoverArtifactsOnly,
RetryAgent,
FreshAgentSession,
...
```

No `None` member was visible in the reviewed enum. This must be fixed or disproved first.

## F02: Blazor revalidation/recovery template boundaries are too permissive

The Blazor delivery template correctly marks architecture/intake as read-only and implementation as mutable. However, the reviewed template also gives `MutateProductTarget` and `ExternalProductTargetMutable` to steps that are named or described as revalidation, results-after-repair, or escalation. Those steps should generally be read-only validation, managed artifact writeback, or external-action controlled writeback.

For the upcoming Tetris test, this matters because a QA/revalidation agent must not "fix" the game during validation. It should branch to a repair step.

## F03: Non-Blazor templates are not migrated

The manifest contains 21 process templates, including business, customer onboarding, incident response, release readiness, OSS governance, and software delivery. A sampled non-Blazor template (`customer-onboarding`) does not include typed `AllowedOperations` or `OperationTargetScope` fields on its steps.

This creates mixed runtime behavior: Blazor templates get typed policy, while other templates fall back to heuristic inference.

## F04: Processes API skill is present but shallow

`codex/skills/candoitall-api-processes/SKILL.md` documents endpoint groups, but it does not explain the new governance fields:

- `AllowedOperations`
- `OperationTargetScope`
- `ContractMode`
- `BlockCause`
- `BlockReasonCode`
- `RecoveryOptions`
- `NextRecoveryAction`
- `ProjectionLineage`
- `ProjectionIdentityHash`
- `WorkflowOutputId`
- `WorkflowOutputName`
- `WorkflowOutputKind`
- `SubprocessChildArtifactExpectationId`
- how to prepare a process run for Blazor WASM PWA/Tetris

## F05: Project-structure writeback tools need explicit policy classification

Blazor templates instruct agents to use `project_structure_asset_create` and `project_structure_node_create`. The reviewed `AgentToolInvocationPolicyMetadata` lists many process and workspace tools, but `project_structure_*` tools were not visibly registered/classified there.

If these tools are treated as generic read tools or not bound to `ExecuteExternalAction`, agents may bypass intended operation contracts.

## F06: Manual/API transitions may still be weaker than automation finalizer validation

`ProcessesService.Runtime.StepTransitions.cs` contains an in-method `ValidateRequiredArtifactsForCompletion` that checks required artifacts by kind, sensitivity, trust, id/title. This is weaker than finalizer-grade validation that checks content, lineage, producer mode, current-run binding, placeholder signals, and managed evidence.

Manual/API transitions must not be a softer path to mark a step completed.

## F07: Template pack metadata still says software process template pack

The manifest is named `CanDoItAll software process template pack`, yet it contains non-software templates. The pack can remain software-oriented, but the generic-process claims and docs should be clear. If this is the core default pack, it should be renamed or split so future users understand which templates are software-specific and which are generic.
