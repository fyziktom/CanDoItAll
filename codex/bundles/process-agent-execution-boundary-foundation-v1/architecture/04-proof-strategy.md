# Proof Strategy

## Required Proof Classes

1. **Dependency proof**
   - MAF has no product-tool module references.
   - Tooling has no product module references.
   - Contracts/abstractions project remains implementation-neutral.

2. **Source movement proof**
   - Direct execution start/detail calls are removed from dispatcher execution partials after SB06.
   - Facade owns the wrapped calls.

3. **Behavior proof**
   - Provider composition tests pass.
   - Process provider parity tests pass.
   - Process outbox tests pass.
   - Receipt semantics tests pass.
   - Artifact lineage tests pass.
   - Process-filtered integration tests pass.

4. **UI proof**
   - N/A unless UI changes.
   - If UI changes unexpectedly, use large-screen PC only.
   - No mobile/small/medium screenshots.
