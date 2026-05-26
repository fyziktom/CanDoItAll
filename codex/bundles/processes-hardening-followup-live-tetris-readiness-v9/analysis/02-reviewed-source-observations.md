# Reviewed Source Observations

## Phase8 head

`processes-hardening` currently points to `phase8` / `4bd0e822a4bef0c0b37187f9810f7e5eb3226061`.

## Enum/read model concern resolved

`ProcessDefinitionEnums.cs` now defines:

- `ProcessDefinitionContractMode`
- `ProcessStepBlockCause`
- `ProcessStepRecoveryOption.None`

This resolves the previously suspected compile issue around `ProcessStepRecoveryOption.None`.

## Tool policy improved

`AgentToolInvocationPolicy.cs` now lists `project_structure_*` read and mutation tools and exposes `IsProjectStructureMutationTool`. `ResolveOperationRequirements` maps project-structure mutation tools to `ExecuteExternalAction`.

Remaining concern: tests must prove every actual project-structure tool exposed by runtime is covered by the policy list. Unknown `project_structure_*` tools should fail closed.

## Process API skill improved

`codex/skills/candoitall-api-processes/SKILL.md` now explains:

- Processes are the durable orchestration layer.
- Workflows are role executors below Processes.
- `AllowedOperations`, `OperationTargetScope`, `ContractMode`, `BlockCause`, recovery options, projection lineage, and Tetris checklist.

Remaining concern: runtime agents also need Blazor/PWA/browser/project-structure skills, not only the Processes API skill.

## Blazor app delivery template improved

The reviewed `blazor-app-delivery` template now keeps `revalidate-blazor-repair` read-only and `record-blazor-results` external-action controlled. That is the right shape.

Remaining concern: the actual live test must ensure assigned agents receive tool profiles consistent with those contracts.

## Tetris scenario exists but is not a live-run profile

`baseline-blazor-wasm-pwa-tetris` includes scenario assignments, transitions, artifacts, contract exercises, and recovery exercises. This is useful for regression. For the real UI test, we need a live-run profile that does not pre-complete the process.
