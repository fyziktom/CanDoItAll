# P02a definition writer implementation handoff

Status: source prepared for parent-owned serialized builds/review; no build or test
success is claimed here. P02b durable operation truth and atomic projection remain open.

## Ownership and interfaces

- `ProjectDefinitionWriter` owns create, full replacement and scalar execution; the
  existing facade forwards frozen commands. Public `ReplaceAsync` returns the full
  accepted acknowledgement; `UpdateScalarsAsync` returns the scalar-only acknowledgement.
  Both accept the existing optional source authorization after the cancellation token.
- Existing `SaveAsync`, `SaveEditorAsync` and creation/reservation/compensation overloads
  delegate. `SaveScalarsAsync` forwards the scalar command. No facade supplies a later
  current revision. The parent owns coherent read wiring and caller conversion.
- `ProjectDefinitionRules` owns pure normalization, current schema field lengths, enum
  validation and duplicate/empty/create child-ID rules. Persisted ownership is validated
  against owner-loaded dictionaries before collection mutation; matching is linear.
  Blank persisted options remain subject to identity validation and are then deleted.
- `Project.ConcurrencyToken` is application-managed and mapped as an EF concurrency token.
  Every accepted definition write explicitly changes it, including child-only and
  normalized no-change saves under a frozen clock. Existing writes set its original value
  from the submitted expectation; acknowledgements capture the saved value in-transaction.
- `ProjectCreationFingerprint` is the extracted shared creation/compensation witness.
  Domain v2 includes the root revision, so an accepted later write cannot remain eligible
  for creation compensation merely because a frozen clock or equal content hid it.

No new public interface, partial class, project reference, Infrastructure product rule,
or adapter-to-Workbench dependency was added. The writer is registered in the existing
Projects composition along with the parent's concrete reader. The create context/result
are internal owner records, not public bypass contracts.

## Transaction and failure behavior

The measured SSI defect and narrow scope decision precede the source edit in
`docs/architecture/concurrent-mutations/01-owner-transaction-design.md` and
`validation/projects-ssi-diagnostic.md`. `BeginDefinitionAsync` is the only new scope
entrypoint. Ordinary mutation scope callers retain their prior behavior. Source-null
PostgreSQL definitions use sorted existing database advisory guards and Read Committed;
they do not take the generic process stripes. Authorized source operations preserve
source-first Serializable acquisition and final source/lifecycle rechecks. InMemory
retains the existing striped test path.

Known precommit concurrency/serialization failures are classified from a new context
after resource cleanup. A changed original definition returns DefinitionConflict;
an unchanged target or create transaction abort returns DefinitionTransactionConflict.
Missing/recreated/retired targets remain distinct. A different source project's admission
failure propagates instead of being mislabeled as target content conflict. There is no
automatic retry and no general transport/commit-exception-to-rejection conversion.

The scope exposes confirmed commit knowledge internally. Source or context cleanup
failures after a confirmed commit are logged while preserving the exact accepted result.
Both scope and context disposal are attempted. Precommit primary and cleanup failures
remain visible together. Optional activity/search failures remain logged after release.
Search now checks the accepted definition revision before projecting, so a delayed old
projection is explicitly obsolete; its persistence is still optional/non-durable in
P02a and does not satisfy P02b's atomic mandatory projection requirement.

## Validation handed to parent

`ProjectDefinitionRulesTests` in the existing Unit assembly has **14 source cases**:
three Facts plus 11 InlineData rows. Filter `FullyQualifiedName~ProjectDefinitionRulesTests`.
These exercise normalization without input mutation, order/ID preservation in an owned
snapshot, four schema field boundaries, three invalid enums, normalized blank persisted
options, persisted child IDs on create, and empty/duplicate child IDs including blank rows.

Parent-owned PostgreSQL tests cover actual root CAS, coherent read, scalar child retention,
stale full replacement, unknown/foreign child ownership, blank-row deletion, exact old
acknowledgements, source admission, independent connections, commit/cleanup faults, and
comparison of the real performance harness. Build-backed discovery, affected tests,
portability-static and migration/model parity remain pending in this handoff.

The changed tracked files passed `git diff --check` at handoff. No build, test execution,
production migration application or commit was performed by the writer subagent.
