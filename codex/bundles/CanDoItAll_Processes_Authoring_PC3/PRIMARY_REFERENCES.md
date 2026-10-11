# Primary technical references

Checked during the source review on 2026-10-09. These support narrow database/tool semantics,
not an assertion that the proposed product design was executed or that current repository
versions should be upgraded. The implementation architecture is this assignment's design.

- R1 — Microsoft EF Core, Handling Concurrency Conflicts:
  https://learn.microsoft.com/en-us/ef/core/saving/concurrency
  A configured concurrency token compares the update/delete against the originally read
  value. Conflicts require application policy; do not silently overwrite an unreconciled draft.
- R2 — Microsoft EF Core, Connection Resiliency:
  https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
  Connection loss during commit can leave an unknown outcome. Preserve operation identity
  and verify success rather than assuming rollback and issuing an unrelated duplicate.
- R3 — Microsoft EF Core, Using Transactions:
  https://learn.microsoft.com/en-us/ef/core/saving/transactions
  Cross-context relational work must participate in the same actual connection/transaction
  where atomicity is claimed; DI lifetime alone is not transaction participation.
- R4 — PostgreSQL 18, Unique Indexes:
  https://www.postgresql.org/docs/18/indexes-unique.html
  By default NULL values are distinct for uniqueness. Design and test global-scope identity
  accordingly; use the current provider/version's supported constrained-key/index semantics.

The task-local SIGNING_AND_COMMITS.md retains the local-unlock and incremental-signing
requirements without requiring a new shared release. The installed shared signing guidance,
when present, also remains applicable. Source-specific statements and read coverage are in
SOURCES.json. This packaging revision did not perform a new web or repository review.
