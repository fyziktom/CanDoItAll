# processes-hardening-followup-template-ui-readiness-v8

## Status

Prepared for Codex execution.

## Reviewed branch context

- Repository: `fyziktom/CanDoItAll`
- User branch name: `process-hardening`
- GitHub connector-visible branch: `processes-hardening`
- Reviewed head: `phase7` / `ca898eccf32664b60e996bf806a035067675c11e`
- PostgreSQL-only requirement remains active.

## Purpose

This bundle verifies whether the phase7 API/read-model/template work is truly production-ready and prepares the Processes module for the next planned UI test: creating a simple **Tetris Blazor WASM PWA** through the process runtime.

## Most important current findings

1. Potential compile breaker: `ProcessRuntimeViewModels.cs` references `ProcessStepRecoveryOption.None`, while `ProcessDefinitionEnums.cs` currently shows `ProcessStepRecoveryOption` without `None`.
2. Several Blazor template steps still grant `MutateProductTarget` / `ExternalProductTargetMutable` to review, revalidation, writeback, or escalation-style steps where product mutation is not appropriate.
3. Non-Blazor templates remain behind the new typed operation-contract model.
4. The Processes API skill exists, but it is still too shallow for the new governance model.
5. Project-structure writeback tools appear in process template instructions, but the generic tool policy registration/enforcement surface does not visibly classify `project_structure_*` mutation tools.
6. Manual/API step transitions still need proof that they use finalizer-grade artifact validation, not a lighter kind/title/trust check.

## Expected execution style

Execute subbundles in order. Run each refactor checkpoint before continuing. Do not stop after only fixing the compile issue; the next planned UI test depends on template quality and API/skill clarity.
