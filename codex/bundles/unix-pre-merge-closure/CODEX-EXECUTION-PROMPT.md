# Codex GPT-5.6 xhigh execution prompt

You are the senior C#/.NET architecture and remediation agent responsible for
closing the final bounded merge blockers on CanDoItAll Unix adoption.

## Immutable starting point

- repository: `fyziktom/CanDoItAll`
- branch: `unix-adoption`
- reviewed commit: `af9206caf3c09dc25088e388727fda0e1b404833`
- merge target: `development` at `acc1ee4a5484dd98bd1df77f8e060a2a5a3b4c59`
- .NET SDK: use `global.json`
- MAF stable baseline: `1.17.0`
- MAF preview baseline: `1.17.0-preview.260804.1`

Re-resolve the branch HEAD before editing. If HEAD moved, record the new exact
anchor and inspect the delta before applying this bundle. Preserve unrelated
changes. Do not push, merge, rebase, or rewrite history unless explicitly
instructed by the operator.

## Primary objective

Produce a narrowly changed exact-head candidate that is safe to merge into
`development` while explicitly deferring actual macOS validation until after
that merge.

## Mandatory closures

1. Replace wall-clock-based legacy process-plan classification with
   deterministic structured payload classification.
2. Guarantee cleanup when process creation succeeds but ownership-boundary
   attachment or startup identity establishment fails.
3. Safely ingest schema-1 Manager process-registry records that lack the new
   boundary identity without authorizing PID-only termination.
4. Prove the Linux application container has the runtime dependency required
   by the Unix process-group bootstrap.
5. Re-run the bounded MAF 1.17 authority and approval-continuation tests on the
   final source snapshot.
6. Create an exact-source evidence record and a truthful final merge decision.

## Scope exclusions

Do not implement Azure Key Vault, HashiCorp Vault, or other enterprise vaults.
Do not claim Keychain support without actual-host evidence.
Do not require macOS testing before merging into `development`.
Do not redesign Simple LLM Chats in this bundle.
Do not opportunistically refactor unrelated Process, MAF, Workbench, Docker,
or storage code.
Do not run the full stable suite after every subbundle.

## Non-negotiable invariants

- Missing or ambiguous legacy metadata fails closed.
- No old process record may authorize name-only, PID-only, or substring-based
  termination.
- Process start is transactional with respect to lifecycle ownership.
- Existing V2 process-plan hashes and serialized forms remain stable.
- Legacy V1 hashes are verified with the exact V1 canonicalizer before any
  migration decision.
- New plans remain executable only with sealed host capability evidence.
- Package mode remains the default clean-build dependency mode.
- Source comments are English.
- Diagnostics and evidence contain no secret values or unnecessary physical
  host paths.
- macOS remains `ActualHostUnverified`, not failed and not passed.

## Validation budget

Use failing-first focused tests while editing. Build only affected projects
during each subbundle. After F01–F03, run one shared checkpoint. At final F06,
run:

- clean package-mode restore/build;
- focused migration and lifecycle tests;
- runtime portability Unit + Integration catalogs;
- focused MAF 1.17 authority/approval tests;
- one disposable app+database Compose smoke;
- static portability and secret scans;
- `git diff --check`.

Do not rerun the broad stable suite unless the invalidation rules in
`plan/validation-strategy.md` require it.

## Required final result

Write a final report with one of:

- `MERGE READY FOR DEVELOPMENT — MACOS ACTUAL-HOST VALIDATION DEFERRED`
- `NO-GO — <specific remaining blocker>`

A green build alone is insufficient. A missing macOS run alone is not a
NO-GO under the operator's current merge policy.
