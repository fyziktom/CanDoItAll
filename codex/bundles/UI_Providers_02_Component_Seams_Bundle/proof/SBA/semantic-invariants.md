Invariant: P02-LOCAL. Raw source: owner review findings 1-4 and direct topics 1-12; executable proof uses this ID in the portable SBC transcripts.

# Local mutation invariants
Baseline three component regressions failed meaningfully: submitted model alias (both existing/New) and lost first-save committed ID. TRX: .mcp-state/p02-results/p02a-before.trx. Production was unchanged for that run.

Canonical registry commit is SaveChanges when no transaction is active, otherwise successful secret transaction Commit. Before entering persistence, owner cancellation rethrows. Persistence exceptions without proof of rollback/commit remain unconfirmed, except recognized concurrency conflicts. A successful canonical stage makes subsequent disposal, observers and projection secondary failures. Reconciliation invokes observer/cache/catalog repair and canonical reads only, never Save/Delete. Generic imported Save/Delete guards remain; maintenance ownership must be rejected before any connector effect.

Optional expected-token editor extension preserves old unversioned API/seed callers while new UI reads supply tokens. Provided tokens are authoritative preconditions; new UI blocks writes while known-commit token reconciliation is pending.

Fixture correction: first PostgreSQL run selected 7, 6 failed before persistence because the Ollama fixture inherited Responses transport; 1 pre-cancel passed. These fixture failures are not behavioral defect evidence. Set its explicitly required ChatCompletions transport before rerun.
