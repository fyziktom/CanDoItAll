# SB02 Semantic Invariants

- Invariant ID: `SB02-I01`
- Source raw note: `N004`, `N005`.
- Expected behavior: Shared hashing is deterministic UTF-8 SHA-256 hex, while MAF argument formatting stays MAF-scoped.
- Disallowed shallow implementation: Leaving hash and argument formatting private inside `MafAgentRuntime` or moving MAF formatting into shared foundation code.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: `StableContentHashTests` and `MafToolInvocationArgumentFormatterTests` in `bundle://proof/SB02/transcripts/validation.txt`.
- Changed source files: `repo://src/Foundation/CanDoItAll.SharedKernel/Common/StableContentHash.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafToolInvocationArgumentFormatter.cs`.
- Production assertions: Tool invocation descriptions and approval summaries call the formatter, and formatter truncation uses `StableContentHash`.
- Red-team negative case: Invalid JSON argument text must still return an empty summary rather than throwing.
- Downstream dependency check: SB06 finalizer prompt summaries and SB07 runtime approvals use the extracted formatter.
