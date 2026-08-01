# Run .NET architecture design and review subprocess

Launch and observe the .NET architecture design and review subprocess. The child process must classify the app type, draft architecture, independently review the design, and return implementation-ready architecture evidence with any mandatory implementation constraints. This parent step coordinates the subprocess and must not perform product mutation or implementation work.

## Contract
- Inputs: Scope packet, project-structure context, requested .NET deliverable, and acceptance criteria.
- Outputs: Observed child architecture run with app-type classification, reviewed design decision, implementation-ready handoff, mandatory implementation constraints, and unresolved architecture risks.
- Evidence: Child run status, .NET app classification, architecture decision, review findings, implementation constraints, implementation start criteria, and UI/no-UI applicability.
- Operation target scope: `ExternalActionControlled`

Use current-run upstream artifacts before asking for more context. If the feature-intake or project-structure context already names the project, product root, deliverable type, exclusions, and acceptance checks, treat optional gaps as assumptions or risks in the architecture handoff instead of blocking the process.
