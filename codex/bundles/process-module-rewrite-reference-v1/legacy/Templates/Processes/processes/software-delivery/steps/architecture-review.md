# Run .NET architecture design and review subprocess

Launch and observe the .NET architecture design and review subprocess. The child process must classify the app type, draft architecture, independently review the design, and return implementation-ready architecture evidence. This parent step coordinates the subprocess and must not perform product mutation or implementation work.

## Contract
- Inputs: Scope packet, project-structure context, requested .NET deliverable, and acceptance criteria.
- Outputs: Observed child architecture run with app-type classification, reviewed design decision, implementation-ready handoff, and unresolved architecture risks.
- Evidence: Child run status, .NET app classification, architecture decision, review findings, implementation start criteria, and UI/no-UI applicability.
- Operation target scope: `ExternalActionControlled`
