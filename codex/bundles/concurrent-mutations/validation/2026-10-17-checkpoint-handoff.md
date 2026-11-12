# Concurrent mutations checkpoint and continuation

Status: partial implementation checkpoint. The user requested closure to conserve
credits on 2026-10-17. Further implementation has stopped. This is not end-to-end
completion, runtime-policy activation, capacity qualification or broad-suite closure.
The supplied architect archive remains proposal evidence, not execution instructions.

## Accepted and unsuccessful validation

The final C4 chat/caller successor discovered and executed exactly **706 cases: 706
passed, zero failed, zero skipped**. Release builds for Unit, Integration and Web
passed. The complete source and runtime path sets stayed unchanged throughout that
run. Source manifest: `8A1F6222FEF1191B6DB6C4B75BC7F9B9BF210E306AE29767D2F5B95FB68473A7`
(8,961 files); runtime manifest:
`7795BCBE06E9F9DECCD6C1649484D10951ED748AA5F94D11330ED69C7CDD8A4A`
(4,340 files). Parent release `124AD10D34822791375D3D136E061F4005A90AD6DA7339C8C6FDFBCFE7C7E8AA`
accepts the tests and permits development, while explicitly refusing complete resource
closure. See [the detailed result](P04-C4-chat-and-caller-repair.md).

The subsequent new native chat browser proof **failed twice**. Both attempts built,
discovered exactly one Fact, passed static enforcement and preserved exact source/runtime
identities. The first passed delayed-refusal/newer-draft preservation, then timed out
on the Agents navigation selector. Its test-only selector correction reached panel
disposal and navigation, but returning to Chat selected the default agent rather than
the fixture agent; the original other thread was therefore absent. The second failure
screenshot confirms that selection. Neither run proves the remaining navigation and
unconfirmed-outcome scenarios. No product source was changed during these attempts.

To close the previously passing checkpoint without introducing a failing new test,
the two newly authored test files were moved, with exact before/after hash checks, to
`.artifacts/concurrent-mutations/validation/c4-chat-browser-selector-validation/held-source`.
They are preserved for repair, not skipped or counted as passing. Original reviewed
browser source remains in the managed candidate as well. Resume by selecting the
original fixture agent through the actual native UI after tab navigation, then verify
all three scenarios and consumer-drain cleanup. Do not infer persistence across a full
reload or circuit loss from this session-scoped retention design.

Local evidence roots under `.artifacts/concurrent-mutations/validation`:

- `c4-chat-caller-contract-validation`: accepted 706, exact identities, segmented
  process observation and resource review.
- `c4-chat-browser-validation`: first browser failure, source `227EFFF3`, runtime
  `355AA438`; initial observer exceeded its finite bound on preexisting TEMP entries.
  The retained V2 observer covers actual execution and records new TEMP paths against
  the complete baseline; it does not retroactively cover initial discovery.
- `c4-chat-browser-selector-validation`: second failure, source `584E0E71`, runtime
  `6753C439`; V2 observation covers discovery and execution. Failed test source is held
  here, with its ownership and move inventory.
- `checkpoint-closure-validation`: final source bridge, standard Release refresh,
  complete proposed-source portability scan/enforcement and documentation checks.

Browser artifacts remain under `output/playwright/agent-chat-submission-retention`:
`d1909d45d26f4bfc9559e56b0f804cc6` and `0308f6ddb60148e9ad233f8dff271a2b`.
Both screenshots/evidence records were inspected. They are not committed runtime logs.

## Scope retained in MAIN

The checkpoint retains the integrated narrow publication protocol, reviewed caller
and deletion-witness repairs, Resource/deletion owner work, persisted Workspace/Agent
policy editing, and chat original-submission retention. Earlier component, HTTP,
PostgreSQL, migration, browser and performance records remain individually scoped;
their counts must not be added into a claim of one complete passing suite.

Only dormant P03 lifecycle contracts/completion codec and their tests are in MAIN.
The later P03 writer, recovery, native pending store, hierarchy caller, compensation,
cancellation, new Process audit evidence, prior-invocation query and planning-runtime
drafts have not been activated or folded into this checkpoint.

Workspace and Agent policy editing has executed owner/UI/API/migration/browser proof;
the snapshot/refresh/replan choices still do **not** govern the complete actual
Agent/Process/Workflow execution chain. Broad Stable remains incomplete: 16,199
executed, 17 failed and 2,444 unexecuted in its historical run.

## Work preserved outside MAIN

Managed candidate base:
`C:/Users/lucys/.codex/worktrees/concurrent-mutations-runtime/CanDoItAll/.artifacts/concurrent-mutations/candidates`.
These ignored files must be preserved before archiving that worktree. A syntax check
or source peer review is not a semantic build, runtime proof or integration approval.

| Area | Durable handoff or inventory | SHA-256 |
| --- | --- | --- |
| Native lifecycle/page | `p03-native-lifecycle-source/closure-handoff.md` | `A8871AF4AFAC507651D7CAD2E5ABE587E4BB5788818E8F069FE08038F6B1CAAD` |
| Persistence/cancellation | `p03-reservation-cancellation-source/CLOSURE-HANDOFF.md` | `1341BDDC72DFC2637DCDB5DB63D5D1983C6620DD33DB4653ABE81E03D7CDD8A9` |
| Cancellation draft, do not integrate | `p03-reservation-cancellation-source/closure-draft-inventory.json` | `3B89C3CACCC20177B5CE48E4A2413980C0B9DABBA1D42BC3A01135F1C9A435E8` |
| Runtime/process | `agent-prior-invocation-evidence/closure-handoff.md` | `0C08B2FFF44B3AA4568CE1B70D28D156659B0B521D535CD6F72A94FAE280FB78` |
| Hierarchy caller, do not integrate | `p03-hierarchy-agent-source/INCOMPLETE-HANDOFF.md` | `C2B2D65E70E75CC0C8378A09E3527F4A87396B1517C74D796F78706F0B896908` |
| Hierarchy 14 sources/9 predecessor images | `p03-hierarchy-agent-source/incomplete-source-inventory.json` | `559BDB1B9D3710F174FE714656EBF0146A6F5F19C713A5E6CBF4268001D0EBD4` |
| Nine related runtime manifest pins | `p03-hierarchy-agent-source/related-manifest-pins.md` | `D8D2F08E6DBEAB8D8B860428B207D25886CF03CEB185D2512D850E20F7F22827` |

The hierarchy caller lacks bounded same-session prior-creation evidence for multiple
new targets and complete governed recovery/effect binding. Its latest API edits were
not parsed or compiled. Reserved cancellation lacks the actual cancellation owner,
effects dispatcher, caller/DI/schema cutover and proof. The planning draft has three
disconnected partial files and no owner/schema-six/MAF wiring. No Workbench hierarchy
dialog implementation was authored. Their handoffs identify exact prerequisites.

## Resume order and release blockers

1. Repair and execute the held native chat browser test; retain both failures.
2. Integrate one coherent P03 owner/caller/migration slice at a time after completing
   source authority, reservation cancellation, compensation and post-release guards.
   Do not activate incomplete pool/lifecycle schema changes separately.
3. Connect persisted policies to actual Agent admission/checkpoints and immutable
   Process/Workflow propagation, with explicit refresh/replan/approval and unknown-result
   behavior. Complete audit producer coordination and prior-creation evidence review.
4. Finish Workflow/component/global-settings protection and complete original-intent
   recovery, foreign-evidence/import/export handling and external-effect semantics.
5. Resolve measured performance failures and resource blockers, then run final broad
   validation. Shared-project Resource serialization, whole-journal allocation/latency,
   fairness, receipt maintenance and sustained capacity remain unqualified. The proposed
   PostgreSQL SERIALIZABLE hierarchy alternative has no implemented lock removal or
   three-writer cycle/independent-overlap proof; it remains a design hypothesis.

## Resources and commits

The 706 run's 20 exactly attributed SDK logs were retained and removed. No observed
owned process/listener or PostgreSQL delta remained for that run. Six added and two
removed shared TEMP entries have unproven ownership and were left untouched. Both
browser attempts disposed their host/database fixtures and returned to the PostgreSQL
baseline; their resource records distinguish observed process closure from unowned
TEMP deltas. Earlier cleanup rejections and observation gaps remain unresolved.
The dedicated `candoitall-concurrent-mutations-pg18-20261016` fixture is now stopped;
its exact container identity and task label were verified first. Its container/volume
and unresolved baseline data remain preserved. The ordinary instance was untouched.
No global prune, ordinary port-5032 shutdown, tool removal or broad artifact deletion
is authorized by this handoff. The managed source worktree is intentionally preserved.

Signing stays enabled. The stale SPA transport commit was cancelled and retried at
2026-10-17T17:24:22Z; its actual GPG PIN-entry process appeared and was brought forward.
The four-file SPA transport commit completed as **`70c65dd`** with a verified good
signature and clean worktree. MAIN checkpoint signing is the final closure action.
No push, merge, deployment or release is implied by the checkpoint commit.

Final complete-source portability enforcement passes with 15,525 reviewed findings
unchanged. The final standard Release refresh and staged documentation validation pass.
The ordinary staged whitespace check reports only seven extra blank lines at EOF:
three byte-preserved source-inventory JSON records, three Markdown records and
`ResourceMutationKind.cs`. Those bytes are retained to preserve accepted source/proof
identities. A second check ignoring only blank-at-EOF warnings verifies all other
whitespace rules; this does not relabel the ordinary check as passing.
