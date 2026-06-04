# SB09 Semantic Invariants

- Invariant ID: `SB09-INVARIANT-001`
- Source raw note: `RQ-010` Runtime tool-provider ownership must be visible in diagnostics and receipts.
- Expected behavior: Runtime provider attach progress includes provider key/display name/tool count; provider-owned tool invocations tag traces and receipts with optional provider key/name; older receipts deserialize with empty provider ownership.
- Disallowed shallow implementation: Adding fields that are never populated, changing the positional receipt constructor, treating empty provider ownership as invalid, or dropping process receipt guards while adding observability.
- Failing-first test: `MafAgentRuntimeToolProviderComposition` fails if provider diagnostics omit ownership; `WorkspaceFileServiceTests` fails if provider ownership is absent from audit receipts or legacy receipt JSON no longer defaults to empty ownership.
- Passing test: `bundle://proof/SB09/transcripts/dotnet-test-unit-maf-runtime-tool-provider-composition.txt`, `bundle://proof/SB09/transcripts/dotnet-test-unit-workspace-file-service-receipts.txt`, and `bundle://proof/SB09/transcripts/dotnet-test-integration-receipt.txt`.
- Changed source files: `bundle://proof/SB09/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB09/source-assertions/runtime-provider-observability.txt` and `bundle://proof/SB09/source-assertions/process-receipt-required-tool-guards.txt`.
- Red-team negative case: A claimed project-structure asset or node writeback cannot be accepted without the required project-structure receipt; a projected browser receipt copies provider ownership only when the source receipt has it.
- Downstream dependency check: SB10 may start because provider observability and receipt compatibility are recorded, tested, and documented.
