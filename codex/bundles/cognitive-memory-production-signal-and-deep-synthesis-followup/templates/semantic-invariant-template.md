# Semantic Invariant Template

- Invariant ID: `SBxx-INV-001`
- Source raw note: user/request note or reviewed source gap.
- Expected behavior: concrete production behavior, not a status label.
- Disallowed shallow implementation: what Codex must not do.
- Failing-first test: exact test name.
- Passing test: exact test name.
- Changed source files: production source paths.
- Production assertions: producer, consumer, lifecycle, and integration paths.
- Red-team negative case: adversarial behavior that must fail.
- Downstream dependency check: dependent services/tests verified.

## Production Behavior Artifact Matrix

Required when the invariant names a production signal, state, record, or event.

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ArtifactName` | `repo://...` or `bundle://...` | `repo://...` or `bundle://...` | `repo://...` or `bundle://...` | `bundle://proof/SBxx/transcripts/...` |
