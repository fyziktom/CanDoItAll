# C# architecture gate

## Preparation-stage verdict

Pass — implementation not yet evaluated.

## Boundary quality

- Core/Application/Runtime/Persistence/Components have explicit responsibilities and forbidden dependencies.
- AgentFramework.Usage is neutral and store-independent.
- Modules.AgentFramework remains a product integration owner.
- App.Composition remains the runtime/persistence/hosted-service composition root.
- Existing generic LLM/conversation libraries are reused.

## Dependency quality

- Intended ProjectReference direction is explicit.
- No service location/reflection is allowed to bypass the graph.
- Existing unrelated AgentFramework cycles are baselined; no-new/no-enlargement is gated repeatedly.
- Old Modules.LlmChats projects have a named deletion phase.

## Pattern quality

- Ports/adapters and a composite read model fit the independent operational stores.
- Immutable pricing evidence avoids historical repricing.
- Typed producer/selection semantics avoid ChatSessionId/string inference.
- Compatibility redirect avoids duplicate page ownership.
- Central ledger/outbox and dual-write alternatives are correctly rejected for scope/risk.

## Testability quality

- Every critical extraction requires direct tests through its new owner.
- Negative architecture/double-count/legacy/route cases are named.
- Old-owner shrink/removal and no-new-partial are required.
- Browser proof supplements rather than replaces lower-level proof.

## Conditions for execution gates

At CP0/CP1/CP2/CP3/CP4/FINAL, replace this preparation verdict with an implementation verdict for the exact candidate. Any fake separation, permanent facade, cycle, permissive profile fallback, UI persistence reference, guessed cost, or missing direct test is a Fail.

