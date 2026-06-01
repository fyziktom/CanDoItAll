# Run .NET implementation slice subprocess

Launch and observe the .NET implementation slice subprocess for the approved scope and architecture. The child implementation slice owns solution setup, feature/function implementation, tests, and targeted proof. This parent step records child-run evidence and does not mutate product files directly.

## Contract
- Inputs: Approved .NET architecture path, app classification, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Outputs: Observed child implementation slice with reviewable change set, test evidence, blockers, rollout inputs, and parent-ready handoff.
- Evidence: Child run status, change-set projection, validation outputs, output-placement notes, migration steps when applicable, touched-surface inventory, and blockers.
- Operation target scope: `ExternalActionControlled`
