# SB04 Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: F02 Blazor validation/revalidation mutation drift and RQ03 Blazor boundary correctness.
- Expected behavior: Blazor process templates allow `MutateProductTarget` and `ExternalProductTargetMutable` only on `implement-blazor-change` and `repair-blazor-findings`; contract resolution is read-only, validation/revalidation uses `ExternalProductTargetReadOnly` with runtime proof operations, result writeback uses controlled external action, and escalation records a managed process decision without product mutation.
- Disallowed shallow implementation: editing labels only; relying on prose while persisted operation contracts still allow mutation; testing one Blazor template while other `blazor-*` templates drift.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first.txt` found 50 persisted boundary violations before correction.
- Passing test: `bundle://proof/SB04/transcripts/passing.txt` and `bundle://proof/SB04/transcripts/test.txt` prove all five Blazor templates and the production projection regression pass.
- Changed source files: `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`, `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json`, `repo://Templates/Processes/processes/blazor-backend-feature/definition.json`, `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json`, `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`.
- Production assertions: Blazor templates reserve product mutation for implementation/repair steps and keep validation, writeback, and escalation non-mutating in persisted typed contracts.
- Red-team negative case: a validation, revalidation, writeback, or escalation step carrying `MutateProductTarget` is rejected by the boundary audit and projection test.
- Downstream dependency check: SB05 and SB15 can rely on non-mutating Tetris intake/review steps before browser execution.
- Required proof: failing-first boundary audit, passing boundary audit for all five Blazor templates, production-path projection regression test, source assertions, anti-stub audit, changed-file hashes.
