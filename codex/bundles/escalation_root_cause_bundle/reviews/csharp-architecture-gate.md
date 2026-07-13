# C# Architecture Gate

## Required Review Questions

- Does the change preserve project dependency direction?
- Are new contracts placed only where consumers require them?
- Are recovery decisions based on typed diagnostics, retry safety, idempotency, policy, and budget instead of message text?
- Are template execution classes, required receipts, artifact slots, branch outcomes, and subprocess contracts typed?
- Does the implementation avoid adding behavior to the adapter partial cluster when a cohesive service is more appropriate?
- Are errors explicit, actionable, and non-sensitive?
- Are logs actionable and masked where they include paths, prompts, or user-provided values?
- Are negative tests present for shallow success traps?

## Blocking Findings

Any of these blocks progression:

- Runtime references a module project.
- Template validation depends on markdown prose parsing for hard gates.
- A missing receipt, unresolved placeholder, or failed readback is silently ignored.
- Physical file existence is accepted as produced artifact truth when ledger/slot proof is required.
- Safe/idempotent completion-gate failure routes directly to manager escalation before retry budget exhaustion.
- New string identifiers are scattered instead of centralized constants, enums, records, or strongly typed wrappers.
- Partial-class additions increase responsibility rather than isolate adapter plumbing.

## Required Closure Evidence

- Source files changed and why each boundary owns the change.
- Tests that prove positive and negative behavior.
- Dependency/cycle check when project references or shared contracts change.
- Proof that no hard gate was weakened to make the incident pass.

## SB08 Closure Evidence

- `Processes.Templates` owns the typed template execution contract documents and strict compatibility validation because the runtime must consume template metadata without markdown parsing.
- `Processes.Contracts` dependency direction is unchanged; `Processes.Templates` still references `Processes.Contracts`, and no reverse dependency was introduced.
- Negative tests cover prose-only hard gates, unresolved deterministic script refs, and invalid runtime-owned subprocess child outputs.
- Positive loader test proves typed execution contracts materialize from template JSON.
- CodeAnalytics snapshot `snap-20260708195818-85ab0701` reported `cycles: []` for `CanDoItAll.Processes.Templates` and `CanDoItAll.Processes.Contracts`.

## SB09 Closure Evidence

- High-risk process template hard gates now have typed execution contracts in template JSON instead of markdown-only instructions.
- Runtime-owned subprocess parent steps declare `RuntimeOwnedSubprocess` execution class while preserving existing typed subprocess contracts.
- Branch decision steps use `BranchDecision`, and reserved `__default__`/`__error__` keys were migrated to stable `default`/`error` identifiers.
- Six business artifact JSON templates now declare `SemanticAcceptanceContract` with `FileExistenceIsSufficient=false`.
- `Template_compatibility_strict_scan_accepts_full_migrated_template_pack` proves full-pack strict validation has zero contract diagnostics.
- CodeAnalytics snapshot `snap-20260708201501-85ab0701` reported `cycles: []` for `CanDoItAll.Processes.Templates` and `CanDoItAll.Processes.Contracts`.

## SB10 Closure Evidence

- `Modules.Processes` owns capability-aware preflight because it has the assigned agent capability state and provider-composition boundary.
- `Processes.Application` consumes template execution-contract required tool names for launch readiness and launch-plan state without parsing markdown prose.
- Negative tests prove prose/profile-only `.NET Application Developer` assignment is rejected for required runtime tool work.
- Existing scope/path/args/manifest guard tests still prove invalid deterministic tool plans are not collapsed into generic missing capability.
- `AgentFrameworkProcessExecutionAdapter.ResultConversion` preserves capability diagnostics in runtime issue evidence so recovery/rework receives actionable state.
- CodeAnalytics snapshot `snap-20260708203629-184e6305` reported `cycles: []` for the scoped process graph.

## SB11 Closure Evidence

- `Modules.Processes` owns `DotNetSolutionSetupRuntimeExecutor` because it maps process runtime assignments to governed AgentFramework workspace commands and completion gates.
- The executor is gated by `DotNetSolutionSetupToolPlanGuard`; no prompt-only deterministic setup is executed unless typed script, manifest, receipt, path, and readback contracts are present.
- Existing project repair is idempotent: existing solution/app targets emit `RuntimeOwned:IdempotentSkip` receipts and then run the helper/readback path instead of destructive regeneration.
- Add-test-project coverage proves the same runtime-owned helper path creates/wires the test project and verifies the `ProjectReference` readback.
- Adapter partial changes are limited to plumbing the runtime-owned result through existing materialization, grounding, completion gates, acceptance, and result conversion.
- Negative tests cover helper failure and readback failure, proving missing receipt/readback evidence is not silently accepted.
- CodeAnalytics snapshot `snap-20260708212205-c7d874cd` reported `cycles: []` for `CanDoItAll.Modules.Processes` and `CanDoItAll.Processes.Application`.

## SB12 Closure Evidence

- Final validation covered focused unit, strict template, integration, incident-equivalent, solution build, anti-stub, and source assertion proof.
- `Modules.Processes` remains the adapter/runtime-owned executor boundary; no runtime project references the module.
- Safe/idempotent recovery semantics remain typed and policy-driven; final incident-equivalent tests prove retry before manager escalation and budget-exhausted root-cause packets.
- The live blocked 5032 instance was not mutated; equivalent local reproduction preserves the original evidence while proving the same failure class.
- CodeAnalytics snapshot `snap-20260708214607-6650a5f9` reported `cycles: []` for the scoped process module/application/contracts/persistence/runtime/template graph.
- The only broad build warnings are known `NU1903` advisory warnings for `Microsoft.OpenApi` 2.0.0; no build errors or architecture-blocking diagnostics were introduced.
