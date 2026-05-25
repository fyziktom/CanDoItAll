# Processes Hardening Follow-up: Runtime Governance, Typed Contracts, and Scenario Resilience

## Status

Prepared for Codex execution.

## Branch Context

The user referred to `process-hardening`, but GitHub exposed the branch as `processes-hardening`. This bundle is prepared against:

- Repository: `fyziktom/CanDoItAll`
- Branch: `processes-hardening`
- Observed head: `474708e7a09d85a90d9541946e1e0e3dd964ec18`
- Commit message: `phase4`

## Mission

The previous hardening phases fixed several critical process runtime issues. The next work should make the process engine more explicit, typed, and resilient:

- Persist step operation contracts instead of deriving them mainly from text.
- Enforce allowed operations, not only a product-mutation boolean.
- Ground external targets through trusted typed sources instead of broad text scraping.
- Validate artifacts through storage abstractions and typed lineage.
- Make workflow/subprocess outputs map to process artifact expectations explicitly.
- Add runtime invariant auditing so policy bypasses are detected even if a tool path escapes policy.
- Improve blocked/failed recovery so process runs do not stall silently.

## Non-negotiable Boundary

Do not confuse `Processes` and `Workflows`.

- `Workflows` are part of the Agents/MAF layer and can be assigned as an executor for a process role.
- `Processes` are above workflows. Processes own step runs, role bindings, artifact contracts, finalization, transitions, recovery, branch dispositions, and governance.

## Expected Execution Style

Execute subbundles in order. After each subbundle, update its proof manifest and semantic invariant file. Do not mark the bundle complete until SB10 red-team validation passes.

## Validation Summary

This bundle was prepared outside the working copy. Codex must copy it into `codex/bundles/processes-hardening-followup-runtime-governance-v5`, run the prepared-bundle validator, and then execute the subbundles.
