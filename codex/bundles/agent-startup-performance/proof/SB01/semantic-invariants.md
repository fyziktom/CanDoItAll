# SB01 Semantic Invariants and Architecture Decision

## Shipped behavior

Only repeated policy construction inside EnsureDirectoryTree changes. A known policy can be reused for an already-existing descendant only when the initially resolved path is equal to the root or begins with its separator-terminated prefix using ordinal comparison.

Every currentPath in that branch is constructed from that literal root and its descendant segments. Therefore containment does not depend on Sensitive versus Insensitive comparison. This is the safety argument; neither synchronous execution nor workspace locks are claimed to prevent noncooperating filesystem edits.

Unknown facts, new directories and case-variant input keep fresh factory acquisition. Operation entry, coordination boundaries, post-write/external-callback precommit acquisition are unchanged. Per-segment pre/post EnsureSafePath, native-root/reparse checks, and private permissions remain.

No global cache, public interface, schema, new project, production helper type, new partial or runtime progress callback changes exist. Ownership stays in Infrastructure; dependency direction and factory consumer semantics stay unchanged.

## Durability and errors

WriteStreamAsync payload stream options, FlushAsync, Flush(true), coordination lock, cancellation, commit/rename and exception/cleanup paths are byte-for-byte outside this edit. Stage-observer assertions prove the existing sequence remains; they are NOT a count of operating-system flush syscalls.

Existing child-process crash, concurrent writers, cancellation-after-flush, backup, root creation and link rejection tests remain. New Linux cases perform actual root replacement/recreation after an awaited external callback and assert explicit errors/no outside writes. New case-fact test controls the existing factory seam to prove reacquisition after callback.

## Evidence distinction and platform capability

Windows cannot rename the active locked root for the new root-swap scenarios; these cases explicitly report that Linux execution is required. Linux must actually execute them and Unix private-mode checks, plus existing symlink cases; a green Windows total alone is not accepted.

Case-variant path behavior requires an insensitive filesystem. It must run affirmatively on Windows; Linux may explicitly report this scenario as inapplicable when its scratch filesystem is sensitive.

A direct Windows fsutil mode-toggle attempt in a fresh owned scratch directory was denied with error0x00000005. No privilege bypass was attempted. Actual Windows flag mutation is NOT claimed. The mode-independent ordinal containment argument and controlled changed-fact callback case are separate evidence, alongside actual Linux same-path root replacement and insensitive Windows/case-sensitive Linux behavior. The independent gate must assess this limitation explicitly.

## Anti-stub and proof requirements

Factory counters wrap the real unchanged factory for depth tests, and payload contents plus commit stages are asserted. Before-change depth failures demonstrate removed work rather than renamed methods. Unknown/callback case-fact tests use the existing internal policy constructor deliberately and do not claim an operating-system mode toggle. Real Linux root/link and Windows case-variant tests cannot be replaced with those controlled facts.

## Stable invariant IDs and evidence mapping

| ID | Invariant | Production method | Test and artifact |
|---|---|---|---|
| SB01-I01 | Exact-root containment does not depend on case mode; variants retain refresh | DurableFileWriter.EnsureDirectoryTree | Existing_exact_descendants_do_not_add_case_probes_or_payload_commits; Case_variant_descendants_keep_fresh_policy_probes; candidate/platform-and-probe-evidence.json |
| SB01-I02 | Every original per-segment path/native-root/reparse check remains fresh | EnsureDirectoryTree; PhysicalFileSystemPathPolicy.EnsureSafePath | Policy_rejects_symbolic_link_ancestor; Mutation_revalidation_rejects_parent_replaced_by_symbolic_link; candidate/linux-unit.trx; candidate/production.patch |
| SB01-I03 | Unknown/create/callback boundaries acquire fresh policy | EnsureDirectoryTree; WriteStreamAsync | Unknown_case_facts_are_reprobed_for_existing_descendants; Creating_root_and_descendants_refreshes_unknown_and_known_case_facts; Callback_case_fact_change_is_reprobed_before_commit; candidate/windows-unit.trx |
| SB01-I04 | Durable payload flush/coordination/atomic replacement/cancellation code remains unchanged | WriteStreamAsync; CommitWithRetryAsync; AcquireCoordinationAsync | candidate/production.patch; Interrupted_write_preserves_complete_previous_content_and_cleans_temporary_file; Cancellation_after_flush_preserves_previous_content; Crashed_process_preserves_previous_content_and_next_writer_recovers_stale_temporary_file; candidate/windows-unit.trx and linux-unit.trx |
| SB01-I05 | Root/link mutation cannot redirect the candidate write outside managed root | WriteStreamAsync post-callback acquisition and RevalidateMutationTarget | Root_link_swap_after_callback_fails_closed_without_touching_outside_target; Recreated_root_after_callback_does_not_receive_a_stale_temporary_payload; candidate/platform-and-probe-evidence.json; candidate/linux-capabilities.log |
| SB01-I06 | Physical policy public semantics and dependencies are unchanged | PhysicalFileSystemPathPolicyFactory.Create; path contracts | baseline/source-hashes.json and candidate/source-binary-hashes.json; candidate/production.patch |
| SB01-I07 | Eligible existing-depth probe work decreases without skipping payload commit stages | EnsureDirectoryTree | characterization/unoptimized-new-tests.trx (expected old-code failures); candidate/windows-unit.trx and linux-unit.trx: D0/D6/D12=8 after versus8/20/32 before |
| SB01-I08 | Real dependent workspace locking/recovery remains equivalent | FileSandboxWorkspaceStore existing-run methods (unchanged in SB01) | baseline/windows-integration.trx and candidate/windows-integration.trx:31/31, all nine update fault boundaries and competing readers/writers |

Exact command argument arrays, selectors, cwd, exit codes and original TRX test timestamps are recorded in command-metadata.json. Historical metadata is explicitly marked reconstructed where it was not captured prospectively.
