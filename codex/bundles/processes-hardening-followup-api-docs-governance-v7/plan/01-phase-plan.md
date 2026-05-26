# Phase plan

## Execution order

1. `01-processes-api-surface-inventory-and-schema-parity` — Audit every `processes_*` tool/API endpoint, DTO, request, response, docs and tests. Produce a matrix showing whether each surface includes operation contract, target scope, contract mode, artifact mappings, typed block state, recovery options, and lineage fields.

2. `02-api-tool-models-operation-contract-fields` — Update process definition save/import/export/template/API tool models so `AllowedOperations`, `OperationTargetScope`, and `ContractMode` round-trip through API/tools, not just UI/service internals.

3. `03-api-tool-models-artifact-output-mapping-fields` — Update API/tool models for `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId`; add validation and examples.

4. `04-refactor-checkpoint-a-api-contracts-and-normalizers` — Refactor duplicated process contract mapping/normalization into shared services and run focused tests before continuing.

5. `05-process-skill-and-documentation-update` — Find and update related process skill(s), Codex skill docs, process API docs, and template authoring docs for new runtime governance fields.

6. `06-template-migration-beyond-blazor` — Migrate non-Blazor templates and examples to typed operation contracts, artifact recovery policy, workflow/subprocess mappings, and contract mode.

7. `07-authoritative-grounding-ledger-policy` — Make `agentProcessGroundedTargetAliasLedger` authoritative for tool policy; resolve alias overlaps and remove heuristic authority drift.

8. `08-projection-identity-hash-dedupe-proof` — Ensure `ProjectionIdentityHash` is persisted, unique, computed from normalized lineage/content identity, and used for dedupe in all projection paths.

9. `09-unified-artifact-validation-service` — Extract finalizer-grade artifact validation into a shared service used by automation finalizer and manual/API transition paths.

10. `10-refactor-checkpoint-b-artifact-lineage-validation` — Refactor artifact projection, lineage, content reading, and validation services for maintainability before adding more recovery logic.

11. `11-typed-block-cause-and-recovery-router` — Extend transition requests and runtime code to carry typed block reason code and recovery options; treat reason-text inference only as legacy fallback.

12. `12-workflow-subprocess-output-mapping-enforcement` — Require explicit workflow/subprocess output mappings for required artifacts and block ambiguous mappings deterministically.

13. `13-script-side-effect-manifest-and-post-execution-audit` — Add script manifest and post-execution diff/fingerprint audit for governed process steps using scripts.

14. `14-refactor-checkpoint-c-recovery-health-api` — Refactor recovery router, block state, and health models into maintainable services; update API query models.

15. `15-process-health-dashboard-api-and-observability` — Expose typed block/recovery state, artifact validation diagnostics, operation contract, and grounding ledger in health/detail APIs and UI.

16. `16-generic-red-team-harness-and-final-closure` — Run generic red-team scenarios across software, business, legal, manufacturing QA, research, and incident-response processes.


## Refactoring checkpoints

- After SB03 run SB04 before continuing.
- After SB09 run SB10 before continuing.
- After SB13 run SB14 before continuing.
- SB16 is the final closure and red-team gate.

## Required validation commands

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~ProcessStepEditorFormTests"
dotnet build CanDoItAll.slnx --no-restore
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-api-docs-governance-v7 -S
```

## Documentation/API/skill audit commands

Codex must adapt paths after discovering the exact files:

```powershell
rg -n "processes_definition_save|processes_run_start|processes_artifact_record|ProcessStepOperation|OperationTargetScope|WorkflowOutputId|SubprocessChildArtifactExpectationId|BlockReasonCode|RecoveryOptions" src codex docs README* -S
rg -n "AllowedOperations|OperationTargetScope|ContractMode|artifact recovery|workflow output|subprocess child artifact" codex/skills docs src/CanDoItAll.Modules.Processes -S
```
