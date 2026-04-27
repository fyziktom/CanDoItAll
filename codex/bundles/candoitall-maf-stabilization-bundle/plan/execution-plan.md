# Execution Plan

## Phase 1: Fix concrete correctness gaps

Subbundles:

- 01 MAF middleware and tool governance
- 02 Structured-output continuations
- 03 Contract validation and repair runner

Goal:

- No machine-critical output loses its contract.
- No disabled or unsafe tool bypasses central policy.
- No invalid output is persisted as success.

## Phase 2: Add stronger decision finalization

Subbundle:

- 04 Finalizer tools for critical decisions

Goal:

- Critical decisions can be submitted through exact-once typed function calls instead of relying only on assistant text.

## Phase 3: Align with MAF orchestration without a rewrite

Subbundles:

- 05 MAF workflows alignment
- 06 Session/history/context stabilization

Goal:

- Keep the process engine, but add MAF workflow/checkpoint/orchestration harnesses at well-chosen boundaries.
- Make session/history behavior explicit and bounded.

## Phase 4: Capability gates and operational visibility

Subbundles:

- 07 Provider capability matrix
- 08 Observability, DevUI/test harness

Goal:

- Runtime behavior becomes explainable and provider limitations are caught early.

## Phase 5: Cleanup and closure

Subbundles:

- 09 Runtime domain neutralization
- 10 Docs/tests/release gates

Goal:

- Generic runtime remains generic.
- Documentation and tests enforce the architecture.

## Required commands in the real repository

Codex must run the highest relevant subset of:

```bash
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If focused tests exist, run them too, for example:

```bash
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "Agent|Maf|Process|Output|Tool|Approval"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "Process|AgentFramework|Maf|Approval|Structured"
```

If the repository uses different test project paths, Codex must discover and run the equivalent tests.
