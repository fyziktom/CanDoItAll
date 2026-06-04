# QA Prompt

Review the implementation against the bundle, not only against build success.

Check:

- All artifact/projection/validation behavior is inventoried before movement.
- Refactor gates pass before downstream work starts.
- Required artifact and lineage behavior is preserved.
- No MAF product dependency is reintroduced.
- No Process Core or driver pack was introduced.
- No mobile/small/medium viewport proof artifacts exist.
- No UI change was slipped in without a large-screen-only validation entry.

Reject shallow proof that only counts tools/artifacts without verifying trust status, lineage, expectation matching, duplicate suppression, and negative cases.
