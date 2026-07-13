# SB02 Semantic Invariants

- Invariant ID: `SB02-INV-structured-receipts`
- Source raw note: GPTPro RC1 and RC3 required branch-aware receipt metadata and duplicate-contract reduction.
- Expected behavior: Receipt requirements can express purpose and applicable branch outcome keys without breaking legacy string values.
- Disallowed shallow implementation: Parsing branch applicability from prompt prose or hardcoding one accepted branch in generic code.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `ProcessCapabilityScopeContractTests` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- Production assertions: Contract normalization includes receipt purpose and branch applicability in the durable receipt key.
- Red-team negative case: Legacy string receipts still parse, so migration does not break old process templates.
- Downstream dependency check: SB03 uses the typed contract metadata to skip acceptance-proof receipts on repair outcomes.
