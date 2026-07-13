# Architecture Checkpoints

## Preparation Checkpoint

- Bundle contains raw input, requirements, source inventory, architecture records, subbundle plans, traceability, and self-review.
- CodeAnalytics snapshot id is recorded.
- No production implementation is included.

## SB01 Checkpoint

- Common MAF workspace image prompts are generic.
- Development/UI-specific image analysis has a documented future owner.
- Tests prove no default prompt assumes software delivery or UI screenshots.

## SB02 Checkpoint

- Runtime capability scope is typed.
- Suppression uses actual deny semantics.
- Required capability requirements flow into `CapabilityAccessEvaluationContext`.
- Provider-level suppression has a stable provider identity selector or remains unavailable with explicit diagnostics.
- Invalid policy cannot fail open.

## SB03 Checkpoint

- Process step scope contract is runtime-neutral.
- Template documents, authoring summaries, launch assignments, persistence, and repair/projection logic carry effective scope.
- Scoped instructions are typed and tied to capability prerequisites.

## SB04 Checkpoint

- Process scope translates to MAF metadata and `AgentRuntimeContextIntent`.
- Prompt fragments and capability policies are generated from one validated contract.
- Metadata parse failures block governed execution.

## SB05 Checkpoint

- Development image analysis behavior is owned by a development project/provider/process capability.
- Common MAF has no project reference to the development owner.
- Domain prompt text scan passes for common MAF.

## Closure Checkpoint

- Targeted unit and integration tests pass.
- `dotnet build CanDoItAll.slnx` passes or failures are documented as unrelated and approved.
- Dependency scan shows no forbidden references or cycles.
- C# architecture gate is passed in `reviews/csharp-architecture-gate.md`.
