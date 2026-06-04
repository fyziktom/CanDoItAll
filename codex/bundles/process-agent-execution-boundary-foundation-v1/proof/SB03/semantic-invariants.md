# SB03 Semantic Invariants

## Invariant SB03-RQ004

- Invariant ID: `SB03-RQ004`
- Source raw note: "Design execution seam" and "Define a small process automation execution client/facade and migration cutline."
- Expected behavior: The bundle defines `IProcessAutomationExecutionClient`, the temporary AgentFramework DTO pass-through rule, the exact dispatcher calls to move, and the explicit out-of-scope surfaces before source movement.
- Disallowed shallow implementation: Naming a facade without method shape, registration rule, movement boundary, or exclusions.
- Failing-first test: N/A - no production behavior changed in this process design gate; `bundle://proof/SB03/transcripts/direct-call-cutline-scan.txt` provides the adversarial source surface that the cutline must cover.
- Passing test: `bundle://proof/SB03/transcripts/design-cutline-source-check.txt`.
- Changed source files: No production source files changed in SB03; design hashes are recorded in `bundle://proof/SB03/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB03/source-assertions/seam-design-cutline.md`.
- Red-team negative case: A design that omits `ExecuteRunAsync`, `GetExecutionRunDetailAsync`, `ListExecutionRunsAsync`, or provider recovery calls would conflict with the direct-call cutline scan.
- Downstream dependency check: SB04 must turn this cutline into guardrails before SB05/SB06 production movement.

## Invariant SB03-RQ011

- Invariant ID: `SB03-RQ011`
- Source raw note: "Multiple phases and refactor checkpoints" and "Force additional refactoring after several subbundles."
- Expected behavior: Gate A is meaningful because it now has a concrete design and no production movement before guardrails.
- Disallowed shallow implementation: Letting production movement start before architecture guardrails are in place.
- Failing-first test: N/A - no production behavior changed in this process design gate.
- Passing test: `bundle://proof/SB03/transcripts/no-production-movement-diff.txt`.
- Changed source files: No production source files changed in SB03; only bundle architecture/proof files changed.
- Production assertions: `bundle://proof/SB03/source-assertions/seam-design-cutline.md`.
- Red-team negative case: Any `src` or `tests` diff in SB03 would violate Gate A and require reopening the phase.
- Downstream dependency check: SB04 is the required Gate A guardrail phase before SB05 can introduce production facade code.

## Invariant SB03-RQ013

- Invariant ID: `SB03-RQ013`
- Source raw note: "Do not run small, medium, or mobile UI validation."
- Expected behavior: SB03 is architecture-only and records browser validation as N/A.
- Disallowed shallow implementation: Producing unrelated responsive proof while defining a service boundary.
- Failing-first test: N/A - no production behavior changed in this process design gate.
- Passing test: `bundle://proof/SB03/transcripts/design-cutline-source-check.txt`.
- Changed source files: No production source files changed in SB03.
- Production assertions: `bundle://proof/SB03/source-assertions/seam-design-cutline.md`.
- Red-team negative case: Any small/medium/mobile screenshot in SB03 would violate the bundle proof policy.
- Downstream dependency check: SB04/SB11 must continue to record browser proof as N/A unless UI changes.
