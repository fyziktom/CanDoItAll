# SB02 proof artifacts

State: `PASS`

Store portable evidence here:

- `proof-manifest.json`
- `transcripts/`
- `architecture/`
- `behavior/`
- `security/`
- `screenshots/` when applicable
- `hashes.sha256`

Do not store credentials, prompt/response content, binary model outputs, or unredacted logs.
Every artifact referenced by the manifest must exist and have a SHA-256 at completion.

SB02 proof includes exact 18/14/6 list/run transcripts, three clean Release builds, real
PostgreSQL migration/constraint/concurrency evidence, EF no-pending-model, anti-stub and
credential/content scans, before/after CodeAnalytics/reference evidence, every-new-public-type
review, independent architecture review, semantic invariants, changed-file inventory, and
SHA-256 inventory. No broad, browser, network, paid-provider, or multi-instance lane ran.

SB04 later invalidated the invocation schema/usage slice and then restored trust with fresh exact
18/14/6 reruns plus EF no-pending-model proof. The additive chronology is
[`architecture/sb04-downstream-invalidation-revalidation.md`](architecture/sb04-downstream-invalidation-revalidation.md);
it preserves both the original SB02 evidence and the sandbox-only deletion bootstrap failure
before the approved 6/6 rerun.
