# Assumptions And Risks

## Assumptions

- Existing public behavior must remain compatible unless a subbundle documents a deliberate behavior correction.
- The first implementation pass should keep extracted types in `CanDoItAll.AgentFramework.Maf` unless dependency analysis proves a new project boundary is needed.
- Existing tests around provider diagnostics, runtime tool provider composition, input attachments, finalizer behavior, and handoff smoke are enough to seed characterization, but new isolated tests are still required.
- Architecture guard tests may use source assertions for forbidden partials and large-type ownership checks, but behavior tests must assert observable outcomes.

## Critical Path Risks

- The critical risk is fake separation: the code may compile after extracting classes, but production behavior may still route through the old large runtime/composer/factory/plugin types.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Extraction happens by file size instead of responsibility | Creates another broad coordinator and repeats the current problem | SB01 responsibility inventory must be accepted before edits. |
| `MafAgentRuntime` becomes a facade in name only | Runtime still owns execution decisions and remains hard to test | SB02-SB03 require direct unit tests for extracted drivers and source assertions that runtime delegates. |
| `RuntimeCapabilityComposer` partials survive as final architecture | Partial-class anti-pattern moves instead of disappearing | SB05 closure blocks on no `partial class RuntimeCapabilityComposer` final boundary. |
| Core runtime keeps using `IServiceProvider` as a service locator | Dependencies stay hidden and tests still require a container | SB04 and SB07 require explicit constructors/factories and a service-locator source assertion. |
| Workspace tool extraction breaks tool metadata or access policy | Agents lose or over-gain tool access | SB06 requires characterization tests and negative policy tests before extraction. |
| Project-boundary extraction creates cycles | Build or architecture direction breaks | SB07 requires before/after `.csproj` and CodeAnalytics dependency checks before new references. |

## Validation Risks

- Positive tests that only assert non-empty tool lists can pass fake separation. Critical subbundles require negative tests and source assertions.
- Integration smoke alone is not enough. Each extracted owner needs a direct unit test that does not instantiate `MafAgentRuntime`.
- Performance claims are invalid without timing evidence. Use existing `IMafRuntimeCompositionMetrics` and build/test timing transcripts where possible.
- Host-visible workspace command behavior cannot be proven by pure unit tests; SB06 requires host-level smoke when command execution behavior moves.

## Reopen Triggers

- A new partial class is added for runtime, composer, factory, or workspace plugin behavior without a temporary removal plan.
- A new extracted type is named `Helper`, `Utils`, `Common`, or broad `Manager`.
- A unit test for extracted behavior constructs `MafAgentRuntime`.
- `RuntimeCapabilityComposer` still owns access planning, descriptor mapping, and attachment orchestration after SB05.
- Adding a new workspace tool family or capability provider still requires editing `MafAgentRuntime` or the old monolithic composer/plugin.
- CodeAnalytics after a subbundle shows member counts did not move meaningfully or new hotspots appeared with the same mixed responsibilities.
