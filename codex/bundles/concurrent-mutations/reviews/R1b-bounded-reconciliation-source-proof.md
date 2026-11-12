# R1b bounded receipt candidate

Status: source authored in the managed continuation checkout under the parent's
explicit release. No build, test, static scan, database operation or performance
measurement was run by this agent. R1c consumers, R1d saved-effect dispatch, R2
producers and runtime freshness settings remain separate work.

The existing Core journal now owns an effects-only origin, append-only approval
history and the bounded receipt slice. Approval append occurs inside its existing
atomic update under the exact live origin lease and current journal. The old prefix
is immutable, duplicate identities and unresolved prior decisions are refused, and
the retained approval bound is unchanged. ProviderRestart checkpoint rules retain
their original meaning. A cancellation summary preserves an already terminal owner
confirmation without querying or disclosing its cached receipt.

Interrupted reconciliation consumes its admitted immutable lease snapshot once,
classifies it once, examines at most four candidates, and publishes confirmations,
cursor and waiting disposition in one conditional update. Exact current run/journal
revisions, identity, state, approvals and active lease fence that update. A failed
comparison does not promote durable state; observed commitment remains a bounded,
nonpersisted sample only under current run read. Missing readers and failed reads
remain visible and never become permission to retry an effect. Current-read denial
or unavailability propagates without new run-bound disclosure. Current-read and background-read failures are wrapped at that policy boundary as a typed read-unavailable error; a later successful observation cannot erase the failure. Requested cancellation and out-of-memory remain unchanged. An unrelated cancellation also propagates rather than becoming a receipt-reader miss. The existing read-policy Fact covers first, provider-internal, post-observation and next-candidate outages whose callback would succeed on a later call; actual owner lookup outages retain bounded ReaderFailure behavior.

`AgentEffectContinuation` is the sole compact Models carrier used directly by Core.
It retains at most four exact bindings and no source, receipt, prepared arguments,
SDK output or disclosure payload. New owner evidence is counted against the complete
8 MiB envelope budget before copying into a candidate; the unchanged 24 MiB journal
bound can refuse persistence. Current operational budgets remain 5 s for cooperative
admission/lookups and 2 s for checkpoint preparation. Existing durable-file commit
completion remains noncancelable after its intent has been written.

The Process receipt path takes an actual pinned claim, validates its exact saved
source, creation time, execution/agent/step and material fingerprint, then calls the
current Process owner query without reloading the full execution record. Current
source and target read checks remain; the resulting Workbench access always has
`CanWrite = false`. Existing ordinary and cancelled lookup paths keep their existing
GUID reader. No source evidence supplied as JSON selects the new path by itself.

Source inventory is **24 rows**, with no dynamic expansion or Skip:

- `AgentToolEffectJournalIntegrationTests`: 19 Facts and two two-row InlineData
  theories, **23 rows**, across the base, Origin, Receipts, Slices and Dispositions
  files. Four rows were the prior R1b-first subset.
- `ProcessCatalogAuthorityPersistenceTests.EffectReconciliation.cs`: one Fact,
  `Expired_write_source_with_current_read_can_confirm_an_original_effect`. It uses
  an actual registered tool and accepted PostgreSQL owner effect, expires its claim,
  revokes write, reconciles under current read, then proves read revocation does not
  remove the saved confirmation. A receipt-path execution reader refuses every full
  run read, and the new original-evidence observation is counted.

The six bounded-slice Facts use independent file stores, actual leases, real atomic
updates and the existing durable commit-boundary fault hook. The maximum fixture
uses separate 32,768-proposal and near-24 MiB shapes; it does not claim both maxima
fit simultaneously. Existing JSON diagnostics distinguish physical read bytes from
deserialization and content-validated cache hits. The measurement records two
logical durable intents and the actual multiple JSON writes rather than calling a
slice a single physical write. Small and maximum cases are unmeasured; the current
budget is not increased to manufacture a passing result.

Parent validation must build the owned Projects/Process/Workbench/MAF dependencies,
derive exact discovery before execution, run the 24 total rows (4 prior and 20 added) and retained compatible
R1a journal coverage, then run and review the mandatory full static procedure.
The broader 74-row Integration selection includes the existing 50 rows plus these
24; the Process Fact requires the already owned PostgreSQL fixture. R1c's proposed
28 rows are not included in this count.
