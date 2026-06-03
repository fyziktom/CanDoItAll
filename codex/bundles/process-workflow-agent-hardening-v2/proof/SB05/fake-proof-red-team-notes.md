# SB05 Fake-Proof Red-Team Notes

## Old V1 Proof Rejection

The checker rejects the old V1 SB08 proof because it contains the exact bypass pattern this bundle was created to close:

- Manual transition/suppressed automation path in the script.
- Harness-owned generated application paths recorded as `AppPath`.
- Missing tool receipts.
- Empty or absent provider execution-run proof.
- Provider usage reported as unavailable instead of observed.
- Missing generated-source-root and layout proof.
- Missing browser summary files in the new required shape.
- Harness `dotnet new` scaffolding transcripts under scenario proof folders.

Transcript: `bundle://proof/SB05/transcripts/expected-failure-v1-sb08-proof.txt`.

## New Proof Acceptance

The same checker accepts the SB04 proof only after every scenario supplies current-run execution runs, receipts, provider usage, generated-root/layout, build, and browser proof.

Transcript: `bundle://proof/SB05/transcripts/passing-new-sb04-proof.txt`.
