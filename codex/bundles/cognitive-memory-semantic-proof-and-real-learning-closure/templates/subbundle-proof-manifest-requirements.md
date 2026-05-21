# Subbundle Proof Manifest Requirements

A completed critical subbundle must include:

- Portable `repo://` and `bundle://` references only.
- Changed file SHA-256 hashes using portable paths.
- Failing-first transcript with non-zero exit code before production fix.
- Passing transcript with focused tests and any affected suite.
- Source assertions that map behavior claims to production code.
- Anti-stub audit.
- Production behavior matrix for new signals/states/records/events.
- Claim-to-code matrix when the execution report uses semantic capability labels.
- Moved-checkout completed-stage validation for final closure.

