# Memory presentation policy

`MemoryProvidersPageController` is shared by the real route and the sandbox. It owns draft
origins, read lanes, admission and observed outcomes; it does not implement stores or drivers.
Refresh and tab changes retain profile, query, feedback, ingestion and raw transport input.
Explicit selection/reset retires the old draft. Up to 32 action receipts retain pending,
unknown or observed facts; unresolved receipts are never evicted to admit another write.

Provider IDs are ordinal. Editing an ID saves at that captured destination; it does not
rename the original. Same-destination submissions are serialized, while independent targets
can progress. Reads are fenced by request, draft, selection revision and database origin.
Retired effects retain their original facts without changing the current tab or editor.

Refresh reads snapshots only. Unknown work stays blocked until explicit exact-identity review;
a missing query operation ID cannot be recovered by a name match. Review observes current
persistence, not proof of the earlier command. Demo review reports which fixed IDs exist;
subsequent creation skips confirmed IDs. Returned identities survive failed read-back.

See [the boundary and proof record](../../../docs/architecture/memory-ui-boundary.md).
