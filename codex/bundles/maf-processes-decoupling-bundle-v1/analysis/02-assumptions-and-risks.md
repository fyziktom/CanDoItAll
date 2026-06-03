# Assumptions And Risks

## Working Assumptions

- `Microsoft.Extensions.AI.AITool` is acceptable in the new tool-provider seam because the seam is runtime-tooling specific, not process-domain specific.
- A small new project such as `CanDoItAll.AgentFramework.Tooling` is preferable to adding `Microsoft.Extensions.AI` to `CanDoItAll.AgentFramework.Core`.
- Processes module can reference the new tooling abstraction project without creating a cycle.
- MAF can reference the tooling abstraction project and remain independent of Processes.
- Existing process tool DTOs can initially remain in the Processes module to avoid premature contract extraction.
- The current `ProcessAgentRuntimeToolProvider` may still return DTO types from Processes. That is acceptable because MAF only sees `AITool` descriptors and does not compile against those DTOs.
- Full process contracts/core extraction is a later bundle.

## Critical Path Risks

- Service lifetime mismatch: MAF is registered both through hosting and module service registration. Tool providers must not capture scoped process services inside a singleton runtime incorrectly.
- Tool parity loss: one existing process tool can be accidentally omitted during migration.
- Approval policy drift: read and mutation tools can change approval behavior when moved out of MAF.
- Hidden dependency: MAF can still reference Processes through `using`, project reference, static helper, or reflection.
- Test weakening: existing tests might be updated to pass by deleting assertions rather than preserving behavior.
- Build graph surprise: adding a new project requires `CanDoItAll.slnx`, affected test csproj references, and README updates.

## Validation Risks

- Unit-only proof is not enough. The migration must prove both "MAF without Processes works" and "MAF with Processes still attaches process tools".
- A static assertion that the project reference is gone is not enough; grep source and build output must prove no `CanDoItAll.Modules.Processes` namespace remains in MAF.
- Process tool parity must compare explicit expected names, not just counts.
- Mutation approval proof must exercise or statically assert `ApprovalRequiredAIFunction` wrapping after migration.

## Reopen Triggers

Reopen the relevant subbundle if:

- a later subbundle needs to reintroduce `CanDoItAll.Modules.Processes` into the MAF project;
- any process tool disappears from the runtime tool surface;
- a process mutation tool can be invoked without approval in interactive mode;
- process read tools start asking for approval;
- MAF can no longer run without Processes registered;
- existing process automation smoke no longer records current-run artifacts or tool receipts.
