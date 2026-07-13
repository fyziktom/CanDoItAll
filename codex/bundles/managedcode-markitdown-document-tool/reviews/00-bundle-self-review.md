# Bundle Self Review

## Status

- `ImplementedWithFollowUp`

## Architecture Gate

- Dependency direction is acceptable: Core owns abstractions; Tools.Documents owns concrete SDK dependency; MAF runtime remains a consumer.
- No runtime partial class expansion is planned.
- No quotation-specific business logic is included.
- Testability is explicit through fake converter and direct converter tests.

## Risks

- ManagedCode.MarkItDown restore/build compatibility was confirmed in the affected projects.
- PDF output quality depends on the upstream converter and PDF text extraction quality.
- The live 5032 validation found an approval continuation blocker after `project_structure_node_create` was requested and approved in the UI. The server did not continue to execute the approved node-create tool call.

## Final Self Review

- The concrete SDK dependency stays in `CanDoItAll.Tools.Documents`.
- Core owns only typed request/result abstractions and does not reference the concrete converter package.
- Runtime wiring uses DI and fallback resolution; no new `MafAgentRuntime` partial-class implementation was added.
- Unit tests cover direct conversion, converter failure, artifact-service success/failure, image rejection, and long receipt target path handling.
- The previous Python command contract was removed instead of kept as a silent fallback.
