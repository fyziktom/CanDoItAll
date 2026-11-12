# Selected authority contention investigation

The selected authority port is functionally integrated; runtime-policy activation and
capacity qualification remain open. Both failed cohorts below preserve unchanged
source/runtime verification, test records, measurements and successful fixture cleanup
in `proof/2026-10-19-planning-authority`.

`c4-selected-publication-cost-validation` failed: both catalog grant writes conflicted
with independent run revisions. Six unrelated writers nevertheless completed while
the real PostgreSQL Project lock was held. Writer elapsed times were 625–1,157 ms.
The publisher refused after its bounded SQL wait; this sample could not establish all
effect assertions because the grant assertion failed. Cleanup removed its database
and workspaces. This is retained as failed evidence, not a capacity pass.

`c4-catalog-dependency-validation` makes catalog-only dependencies explicit and rereads
the workspace index after admission. The four new file-lock race cases pass: independent
run work is preserved, expected workspace revision still refuses, intervening catalog
changes are not rebased, and pending shared publication remains a refusal. Existing
selected authority, publication validation/recovery/budget and all ten Agent policy
owner cases pass. In total 36/38 functional cases pass; two older test arrangements
still call Guid-only deletion, which P03 now explicitly refuses. Their setup must use
the original-intent lifecycle driver without changing grant assertions.

In the second SQL cohort all six writers succeed, all finish while SQL remains blocked,
zero task/receipt effects are verified, and all file/SQL guards are reacquired. Grant
times improve to 395/730 ms. Checkpoints take 620/655 ms. Cancellation takes 1,101/1,146 ms
in independent/same workspaces, failing the unchanged one-second criterion. The roughly
816 KiB seeded catalogs and process-wide 252 MB cohort allocation are relevant context;
they do not establish the cause. Existing owner activities will be recorded in a third
cohort before choosing another production optimization. Nested durations cannot be
summed as total latency. No warmup, relaxed threshold, hidden retry or smaller catalog
has been introduced.

CodeAnalytics `snap-20261019023033-a1c12fef`: 408 documents, 1,730 types, 3,516 dependencies,
four unchanged type cycles, no new project reference or non-advisory workspace errors.
Portability enforcement passes at 15,604 reviewed findings without baseline changes.
The catalog-only correctness gate passes; the SQL responsiveness gate remains open.


## Profile-directed existing-directory repair

The diagnostic successor repaired the two old deletion setup calls; both passed with unchanged production inputs. Its cost cohort still failed. Removing the empty case-probe flush then passed all 33 path/durability and 38 catalog/authority cases, but the latency criterion still failed (cancellations 1,145–1,350 ms). Neither earlier experiment was a capacity pass.

The complete sampled profile and two-pass source review are retained in [the performance review](2026-10-19-file-publication-performance.md). A failed profiler launch and a trace truncated on host exit remain recorded separately. The successful bounded profile stopped before host exit, resolved method stacks and reported zero lost events. It pointed to repeated ancestor walks inside synchronous existing-directory preparation.

The [reviewed repair](../plan/P04-existing-directory-cost.md) adds an existing-directory no-op only after fresh full path validation, exact known-case containment and no private-permission request. It changes no payload durability, lock, pre-commit revalidation or recovery rule.

`c4-directory-fastpath-validation` passed **72/72**: 33 physical-path/durable-writer, 38 catalog/authority and one unchanged SQL cost case. Standard Unit and Integration Release builds passed, confirmed discovery was 33/38/1, complete portability enforcement passed at 15,604 protected findings, and rendering/documentation/diff checks passed. Source/runtime identity remained unchanged during execution. No owned process or database-name delta remained; eleven unowned TEMP deltas were preserved.

| Concurrent writer | Same workspace (ms) | Independent workspace (ms) |
| --- | ---: | ---: |
| Checkpoint | 443.0 | 432.7 |
| Cancellation | 958.8 | 920.2 |
| Grant update | 674.8 | 551.0 |

All six completed while the actual five-second SQL holder remained present. Publisher refusal, zero Tasks/Receipts, guard reacquisition and fixture cleanup passed. This meets the declared small forced-wait criterion once, with limited headroom; it is not p95, maximum-size, sustained-load or fairness qualification. The retained result still sets `CapacityQualified=false`.

Exact passing proof: [c4-directory-fastpath-validation](../proof/2026-10-19-planning-authority/c4-directory-fastpath-validation.json). Source digest: `715F92B91F5D5F7D7FBEC05760F34409184DAC8C6AC25684E4643594554D9A62`; runtime digest: `8664F0C5ADB52B83FBF2395476A72F63DC99F66159E87B3C94367E0D913D381D`. Runtime policy activation remains the next operational slice, not a result of these synthetic task-plan authority tests.
