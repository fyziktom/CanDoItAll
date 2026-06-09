# Stable Test Fixture Policy

Long-lived tests may use:
- `tests/**/TestData/**`
- direct source scans
- stable README docs under source projects
- generated API snapshots stored under test data
- deterministic in-memory fixtures

Long-lived tests must not use:
- `codex/bundles/<bundle-name>/...`
- proof transcripts from old bundles
- bundle README text as behavior source
- current execution report rows as production proof

Bundle tests can validate bundle shape, but production architecture/runtime tests must outlive bundle deletion.
