# Proposed Core Expansion For This Bundle

## Production Core candidates
1. `ExecutionEvidence`
   - immutable run/attempt/proof summary facts
   - no execution client, no AgentFramework objects, no provider repair behavior
2. `FinalizerEvidence`
   - finalizer intent/outcome descriptors
   - no finalizer application, no transition mutation
3. `RuntimeProofDiagnostics`
   - typed reason/result descriptors for proof consistency
   - no file IO or transcript parsing in Core
4. `ProviderRetryDiagnostics`
   - pure categorization facts derived by module adapters
   - no provider health calls, no repair save, no retry execution

## Explicitly not moving
- `ExecuteUntilSettledAsync`
- provider fallback health probe
- assigned-agent repair/save
- no-progress journal writes
- artifact recovery execution
- finalizer application service
- transition application
- any EF/workspace/storage behavior
