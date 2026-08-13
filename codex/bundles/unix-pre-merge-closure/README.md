# CanDoItAll Unix adoption — pre-merge closure bundle

## Purpose

Close the last bounded merge blockers on `fyziktom/CanDoItAll` branch
`unix-adoption`, then produce an exact-head decision for merging into
`development`.

## Reviewed anchors

- candidate branch: `unix-adoption`
- reviewed candidate commit: `af9206caf3c09dc25088e388727fda0e1b404833`
- target branch: `development`
- reviewed target commit: `acc1ee4a5484dd98bd1df77f8e060a2a5a3b4c59`
- Microsoft Agent Framework stable baseline: `1.17.0`
- Microsoft Agent Framework preview baseline: `1.17.0-preview.260804.1`

## Decision entering this bundle

`NOT MERGE READY YET — THREE BOUNDED PRE-MERGE CLOSURES REQUIRED`

This is not another Unix-portability implementation phase. The architecture,
Docker deployment, MAF 1.17 integration, process capability model, local secret
fallback, MCP hardening, and Windows/Linux focused validation are sufficiently
advanced to move toward merge.

The remaining blockers are:

1. legacy process plans are partially classified by a wall-clock cut-off rather
   than by their persisted payload shape;
2. a process can survive when process start succeeds but OS ownership-boundary
   attachment fails;
3. schema-1 Manager process-registry records can deserialize without the newly
   required process-boundary identity and fail later during recovery.

macOS actual-host testing is explicitly post-merge work. Enterprise vault
implementations are explicitly out of scope.

## Execution order

1. `F00` — freeze and characterize the exact branch.
2. `F01` — correct legacy process-plan classification.
3. `F02` — make ownership-attachment failure cleanup total.
4. `F03` — migrate legacy Manager registry records safely.
5. `F04` — prove the Linux container process dependency and smoke path.
6. `F05` — rerun the bounded MAF 1.17 authority/approval regressions.
7. `F06` — execute the exact-head merge gate and update canonical bookkeeping.

## Test-budget rule

Do not repeatedly run the approximately eight-thousand-test stable suite.

During implementation, run only the directly affected tests and projects.
At final closure, run one clean package-mode Release build, the runtime
portability Unit and Integration catalogs, the named MAF tests, migration
proof, and one disposable Compose smoke. Run the broad stable suite only when
a change escapes the declared source boundary or focused evidence reveals a
cross-cutting regression.

## Completion state

The desired final decision is:

`MERGE READY FOR DEVELOPMENT — MACOS ACTUAL-HOST VALIDATION DEFERRED`

This wording does not claim verified macOS support.
