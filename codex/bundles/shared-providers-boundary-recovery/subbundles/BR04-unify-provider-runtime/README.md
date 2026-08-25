# BR04 — Unify provider runtime

## Objective

Remove the duplicate direct provider inference stack and make all production prompt/image execution use the AgentFramework/MAF provider drivers through a narrow application port.

## Required inventory

Use the BR00 caller inventory and source search to account for every caller of the legacy stack. Known consumers include Workbench rewrite flows and shared-provider relay.

## Required implementation

1. Establish one neutral execution port, reusing an existing suitable contract or introducing `IProviderPromptExecutionService` equivalent at the lowest correct stable layer.
2. Implement the port over the existing AgentFramework/MAF provider runtime and runtime profile resolver.
3. Migrate Workbench prompt/rewrite execution to the new port.
4. Migrate shared-provider relay to the same runtime path.
5. Migrate any remaining production callers.
6. Delete the legacy direct inference abstractions and implementations:
   - `IProviderAdapter`
   - old `ProviderRegistry`
   - `ProviderExecutionService`
   - old request/response DTOs
   - OpenAI/Ollama/ComfyUI direct `SendAsync` adapters
   - `LegacyProviderRuntimeGateway`
7. Remove DI registrations and registration-order overrides associated with the old path.
8. Preserve useful non-inference logic by extracting it deliberately:
   - capability/manifest validation
   - provider health probes
   - model discovery
   - pricing calculations
   These services must not expose an inference-send operation.
9. Preserve scenario/process mock behavior through the canonical driver/runtime abstraction rather than a side registry.
10. Ensure shared import materialization resolves to the same runtime driver and capability validation as personal providers.

## Runtime invariants

- One execution snapshots one effective provider revision.
- A remote import change cannot mutate an in-flight execution.
- Missing secret/source/driver/capability remains fail-closed.
- Relay rate limit and audit wrap the canonical invocation, not a bypass.
- Cancellation and error mapping remain compatible.

## Focused tests

- Workbench rewrite uses the MAF-backed execution port.
- Shared relay invokes the same port.
- Personal and shared provider profiles select the correct driver.
- Revision snapshot remains stable during execution.
- Missing driver/capability/secret fails before outbound execution.
- No direct adapter is resolved from DI.
- Scenario/mock provider behavior remains available where required.

## Acceptance

- No production legacy direct inference type remains.
- No raw provider-specific inference HTTP request is issued outside canonical drivers/transport.
- Workbench has no Workspace provider execution dependency.
- ProviderManagement remains free of Workspace.
- Affected runtime, Workbench, relay, and host projects build and tests pass.

## Commit

`BR04: unify provider runtime through MAF`
