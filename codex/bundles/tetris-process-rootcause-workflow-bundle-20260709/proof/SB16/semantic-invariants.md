# SB16 Semantic Invariants

## INV-SB16-001 Separate Diagnosis, Mutation, and Acceptance

- Expected behavior: exact failed evidence is diagnosed before mutation; product mutation is performed by a repair engineer; acceptance is owned by an independent reviewer.
- Disallowed implementation: the repair agent waives its own failed proof as residual risk or the parent quality-repair step edits product files.
- Passing proof: child process definition, parent subprocess contract, and `bundle://proof/SB16/transcripts/template-preflight.txt`.

## INV-SB16-002 Bounded Specialist Assistance

- Expected behavior: the first failed independent proof routes to bughunt; a specialist diagnosis guides exactly one second repair; a second failed proof returns no-go.
- Disallowed implementation: repeated identical repair attempts, third mutation loop, or silent conversion of no-go into acceptance.
- Passing proof: finite branch graph in `dotnet-quality-repair/definition.json` and template projection tests.

## INV-SB16-003 Domain Isolation

- Expected behavior: generic runtime/dispatcher sees subprocess and diagnostic contracts only. .NET/UI/browser policy stays in templates and .NET driver/contributor code.
- Disallowed implementation: sample-app names or development-specific branches in generic process runtime/application dispatch.
- Passing proof: `bundle://proof/SB16/transcripts/source-assertions.txt` and `bundle://proof/SB16/architecture-review.md`.

## Closure Result

- Result: `Passed`.
- Runtime preflight: child 9 steps, parent 20 steps, no warnings.
- Provider configuration: 28/28 active agents on `gpt-5.4-mini`.
- Dependency result: zero cycles and zero error findings.
