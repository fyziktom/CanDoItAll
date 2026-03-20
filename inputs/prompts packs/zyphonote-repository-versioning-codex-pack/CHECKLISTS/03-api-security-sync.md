# API / security / sync checklist

- [x] Repository APIs require auth where appropriate.
- [x] Read access respects repo visibility and ownership.
- [x] Write access respects ownership/fork policy.
- [x] Protected default branch cannot be deleted or force-updated.
- [x] Ref updates use compare-and-swap semantics.
- [x] Origin status batch endpoint supports WASM sync.
- [x] Pull batch endpoint can fetch enough data for offline graph/history.
- [x] Commit/push endpoints return useful conflict detail.
- [x] Audit log entries are written for repository mutations.
