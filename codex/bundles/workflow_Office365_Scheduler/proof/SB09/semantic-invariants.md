# SB09 Semantic Invariants

## SB09-INV-ROOT-CAUSE

- Source input: reopened live feedback for the Office365 email summary-to-project workflow.
- Expected behavior: approval cannot produce a false success where the processed category is not applied.
- Disallowed shallow implementation: documenting the issue without changing the policy that caused the workflow to stop before the marker mutation.
- Failing-first proof: `bundle://proof/SB09/transcripts/failing-first-office365-processed-marker-approval-policy.txt`.
- Passing proof: `bundle://proof/SB09/transcripts/passing-office365-processed-marker-policy.txt`.

## SB09-INV-NARROW-MARKER-POLICY

- Source input: email workflows should run without human-in-the-loop for processed-category/label marking.
- Expected behavior: only executors declaring `IdempotentExternalMarker` may write externally without approval.
- Disallowed shallow implementation: setting all scheduler-launched external writes to `ApprovalRequirement.NotRequired`.
- Passing proof: `bundle://proof/SB09/transcripts/passing-plugin-manifest-tests.txt`.
- Production assertions: `bundle://proof/SB09/transcripts/source-assertions-email-marker-policy.txt`.

## SB09-INV-OFFICE365-GMAIL-CONSISTENCY

- Source input: keep current email workflow behavior simple across the existing email plugins.
- Expected behavior: Office365 and Gmail mark-processed descriptors both declare the marker capability and `ApprovalRequirement.NotRequired` in real and bundled descriptors.
- Disallowed shallow implementation: changing only the fake bundled descriptor or only one email provider.
- Passing proof: `bundle://proof/SB09/transcripts/passing-plugin-simulation-tests.txt`.
- Changed source files: `bundle://proof/SB09/transcripts/file-hashes-email-marker-policy.txt`.

## SB09-INV-NO-STUB-REPAIR

- Source input: repair must change production policy, not add placeholder behavior.
- Expected behavior: no new scoped production stubs, TODOs, or `NotImplementedException` paths.
- Disallowed shallow implementation: test-only bypasses or fake-mode-only policy changes.
- Anti-stub proof: `bundle://proof/SB09/transcripts/anti-stub-audit-email-marker-policy.txt`.
