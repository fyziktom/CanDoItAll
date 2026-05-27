# MAF 1.6 Processes Real-Usage Hardening v2

## Status

Prepared for Codex execution.

## Reviewed branch context

- Repository: `fyziktom/CanDoItAll`
- Reviewed branch visible through GitHub connector: `processes-hardening`
- Reviewed head: `update maf` / `bdb85699c439bc7a030098812347e671f3208cfe`
- Previous failed run: `9bbc0667-9d12-4506-ba81-654ef924cad6`

## What was improved

The previous bundle was completed. The repo now references MAF 1.6 packages:

- `Microsoft.Agents.AI` `1.6.2`
- `Microsoft.Agents.AI.OpenAI` `1.6.2`
- `Microsoft.Agents.AI.Workflows` `1.6.2`
- `Microsoft.Agents.AI.A2A` `1.6.2-preview.260521.1`

Process artifact validation is also stronger:

- `ProcessCompletionArtifactValidator` exists.
- Validation statuses include `ContentUnavailable` and `ContentHashMismatch`.
- `RecordArtifactAsync` computes content hashes for managed artifacts when possible.
- `StorageBackedProcessArtifactContentReader` can read storage references and workspace managed files.
- The failed-run `StaleOrWrongRun` class of issue has targeted code and tests.

## Why another bundle is needed

The package upgrade appears real, but the adapter still looks largely like the older design. That can be acceptable as a compatibility pass, but before real tests we must explicitly decide which MAF 1.6 advantages to adopt.

This bundle therefore asks Codex to:

1. prove the upgrade is not only a package bump,
2. integrate the useful MAF 1.6 features where they reduce CanDoItAll process failures,
3. keep CanDoItAll's process governance generic,
4. run a real live process smoke test only after adapter and validation seams are proven.
