# Workspace Storage Catalog UI decoupling

Execute [prompt.md](prompt.md) with Codex GPT-6 Astra Max on the current owner-assigned checkout.
First repair the two related API read-lifetime findings, then extract only Storage catalog
administration. This is neither another Memory extraction nor completion of all Workspace.

Review origin: `components-decoupling` at `56f615a19f5d7eb22042584230ceebcc611bf549`.
The preceding commit archives the previous handoff. Review SHAs are not execution pins.

## Read the complete assignment

[API review](API_REVIEW.md), [Workspace status and next slice](WORKSPACE_STATUS.md),
[dependency architecture](ARCHITECTURE.md), [Storage source review](STORAGE_REVIEW_NOTES.md),
[effect/credential safeguards](SENSITIVE_STATE.md), [acceptance matrix](VALIDATION_MATRIX.md),
[application regressions](APPLICATION_REGRESSION_MATRIX.md), [development loop](DEV_LOOP.md),
[proof limits](PROOF_STATUS.md), [sources](SOURCES.md) and [shared foundation](shared/README.md).
Current repository rules take precedence over stale historical maps.

## Included / excluded

Included: AP-R1 cleanup ownership, AP-R2 current-denial propagation; Storage catalog list,
three-step editor, FileSystem/IPFS/FTP configuration, default routing purposes, explicit Test,
Save/Delete, honest partial outcomes, production composition and independent sandbox.

Preserved but not extracted: Storage placement recovery/owner continuation, cross-module
catalog pickers, Data Sources and database transfer; generic settings-renderer hosts remain
with their owners. Existing Core and API leaf dependencies must not grow toward Storage.
The Recovery button still opens the real production dialog through a host-owned callback.

No storage engine, routing algorithm, authentication system, database schema, broad contract
migration or automatic recovery/retry framework is commissioned. All runtime/file authority
stays with existing owners. A narrowly justified owner correction is allowed only with its
actual consumer and persistence proof.

The reviewer read source and connected metadata, but did not build/run the product. Package
validation is not product proof. Run the utility with `python tools/validate_package.py`.
