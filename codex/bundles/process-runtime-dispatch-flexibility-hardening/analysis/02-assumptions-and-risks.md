# Assumptions And Risks

## Assumptions

- The behavior introduced in `6775de820 phase1` is intended to be preserved unless an execution subbundle proves it contradicts a normalized requirement.
- The first implementation should create one current AgentFramework driver implementation and seams for future model/provider-specific strategies. It should not invent unused strategy variants.
- Generic process runtime should not reference Workbench, ProjectStructure, AgentFramework module hosting, or .NET-specific concepts.
- Completion evidence, prompt fragment composition, provider/tool execution, and driver-specific step execution dispatch should be owned by drivers. Generic dispatch may own scheduling, claims, and lifecycle transitions only.
- MAF/AgentFramework process support must implement Processes driver abstractions from below the Processes dependency boundary.
- The current tests are the best behavioral source of truth for edge cases, but they must be split and expanded so services can be tested directly after extraction.
- UI changes are not expected until SB07 unless extraction breaks dashboard behavior or launch/dispatch API shapes.

## Critical Path Risks

- SB01 is a critical foundation because it decides project placement, driver boundary shape, and what must remain generic.
- SB02 is a critical foundation because adapter decomposition can break subprocess waiting/reuse, agent invocation, structured output validation, managed artifact materialization, and manager signal creation.
- SB03 is a critical foundation because prompt strategy seams must preserve current AgentFramework behavior while enabling non-software and model-specific prompts.
- SB04 is a critical foundation because product completion and receipt policy currently protects against false completions for software-delivery flows.
- SB05 is a critical foundation because domain-specific launch contributors must not leak .NET assumptions into enterprise processes.
- SB06 is a critical foundation because dispatcher claim lifecycle, branch propagation, recovery, and queue behavior can deadlock or duplicate work if extracted incorrectly.

## Validation Risks

- Unit tests may pass after mechanical extraction while behavior remains over-coupled. Critical subbundles require semantic negative and positive tests, not file movement proof.
- Some process flows depend on hosted AgentFramework runtime services and workspace tool receipts; integration proof may require scoped fake services if full host runs are expensive.
- Browser proof is only required if UI/dashboard/API surfaces are modified, but SB07 must still run process workspace smoke checks if launch/dispatch projections change.
- Refactoring may expose stale methods or duplicated code. Those must be removed only after tests prove they are unused or replaced by a typed service.
- File-size and coupling guardrails are not currently enforced automatically for this hotspot. SB07 must add or run an explicit audit transcript.
- There is a subtle design risk in the phrase "runtime process dispatching should be in drivers": moving scheduling, claims, or runtime state transitions into a driver would couple core liveness to one implementation. The bundle defines the split as generic dispatch orchestration in Processes and driver-owned step execution dispatch policy.
- Existing `ProcessRuntimeProjectionQueryService` contains AgentFramework display wording. SB01 must decide whether this remains harmless UI terminology or should become driver-neutral observation labeling.

## Reopen Triggers

- Reopen SB01 if implementation discovers the chosen project boundary creates circular references or requires generic runtime to reference domain modules.
- Reopen SB01 if an implementation proposes any `src/Processes/*` reference to MAF, AgentFramework, `Modules.AgentFramework`, or an MAF-owned driver implementation.
- Reopen SB02 if any adapter test passes only through the old monolithic class or if extracted services cannot be mocked independently.
- Reopen SB03 if prompt composition is not driver-owned or if generic non-software process prompts include .NET, Blazor, repository, project-structure, subprocess launch tool, or AgentFramework finalizer text.
- Reopen SB04 if completion evidence policy is not driver-owned or if product completion can be marked successful without a current-run tool receipt, product mutation proof, or managed artifact grounding when a policy requires it.
- Reopen SB05 if .NET/software-delivery launch variables appear for unrelated business analysis, supplier analysis, reporting, or quality-management process definitions.
- Reopen SB06 if driver-specific step execution dispatch remains in generic dispatcher/application code, or if dispatch can create duplicate branch propagation paths, leaves stale branch methods, loses claim cleanup, or suppresses recovery diagnostics.
- Reopen SB07 if proof is concentrated in one monolithic integration test file without direct tests for extracted services.
