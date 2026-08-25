# SB01 proof artifacts

State: `PASS`

This Governed proof contains:

- `proof-manifest.json`: machine-readable result, commands, exact discovery, and progression;
- `manifest.md`: artifact-backed behavior matrix and failing-first/passing classification;
- `semantic-invariants.md`: invariant-to-source/test/red-team mapping;
- `changed-files.md` and `hashes.sha256`: working-tree inventory and after-state hashes;
- `architecture/`: before/after references and CodeAnalytics, public API/source assertions, and
  the checked independent architecture review;
- `behavior/`: exact protocol, routing, and access-context observations;
- `security/`: trust-boundary and redaction result;
- `transcripts/`: baseline failing controls, Release builds, exact list/run selections, scans,
  diff checks, closure validation, and final working-tree capture.

No credential, prompt/response content, binary model output, private endpoint, or unredacted log
belongs in this tree. Every referenced completion artifact is covered by `hashes.sha256`.
