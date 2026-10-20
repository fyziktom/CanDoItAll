# Process initial Project/root lock ordering

The candidate has **194 distinct passing selected cases** across an exact test-only
repair bridge. The initial run executed 194 cases (185 passed, nine fixture failures);
the corrected 22-case Process admission group passes in full. All production source
and runtime files are identical across the bridge. The
[design](../plan/P07-process-project-lock-order.md) preceded implementation.

## Change and boundaries

Relational initial commits now acquire the complete original Project gate set before
the in-process root sequence leases and PostgreSQL root advisory locks. The small
Process persistence collector includes run, executable closure and saved launch/source
admissions. The existing Runtime policy boundary delegates ordered keys to Projects.
Conflicting lifetimes and an unbound native target fail before acquisition. Global
definitions and root-only runtime updates do not add Project locks. InMemory keeps its
existing path. There is no new project reference, lock implementation, table or migration.

Lock acquisition does not read current Project rows or require current authority.
Duplicate receipt lookup still precedes lifetime, source and executable validation, so
an accepted initial commit recovers after retirement. Scoped initial replay now requires
the configured coordinator and Project policy, which application composition already
provides. A manually constructed read-only unit cannot bypass that boundary.

Link delivery already acquires Project before the prepared receipt row. Prepared launch
continuation updates do not subsequently acquire Project/root locks. The event appender
uses root sequence leases without Project gates. The source-tool preparation path has
no later Project advisory acquisition; the future task publisher must take its Process
fence before staging business rows. This checkpoint does not activate inherited publication.

## Tests and retained repair

Six new PostgreSQL races pass for roots and real child commits beneath a persisted,
claimed parent: publication arriving first, Process arriving first, and cancellation
with no committed rows followed by explicit retry. The competitor uses the actual Project
gate and root advisory protocols; this is not yet the inherited task publisher itself.
A seventh new case proves ordered/deduplicated gates without validating a retired lifetime.
Six new Unit cases cover executable-only Project scope, global definitions, conflicting
lifetimes, root-only commands and original target binding.

The initial 143 Integration cases passed 134; all 51 Unit cases passed. Six failures used
the retired `ProjectsService.DeleteAsync` entry and three expected the previous dependency
error wording. The repair uses the real lifecycle driver and an original captured deletion
submission. Its race observes both session and transaction advisory locks used by deletion
handoff; it still proves actual database blocking through commit. No authority guard was
relaxed. Only the two Process admission test files and their assembly/PDB changed.

The corrected 22/22 group includes every initial failure and repeats all new races.
The other selected cases cover prepared launches, original source authority, canonical
material scopes, publication fences, immutable executable publication and atomic launch
behavior. A first Integration build found a test-local variable-name collision; its failure
and the subsequent passing build are retained. All six affected production/test project
builds pass in standard Release. The repair rebuilds only the changed Integration project.

The complete portability scan includes untracked candidate files. One new `link-reparse`
lexical finding matches the typed Process `LinkTarget`; it performs no filesystem access.
The reviewed baseline adds exactly that finding. Final enforcement passes without a write
flag at **15,642** findings. Baseline/secrets tooling tests, rendering tests/register,
documentation and diff checks pass. The later documentation closure is checked after
releasing the frozen candidate.

## Architecture and limits

Snapshots `snap-20261020042945-de5e7b25` and `snap-20261020043714-de5e7b25` cover six projects:
2,578 to 2,579 types, 21,828 to 21,833 members, unchanged 145 registrations, 31 entities,
822 findings and 208 diagnostics. No new findings or cycles appear. Three existing size/
source-span findings change; the collector is separate from the large runtime unit and
the small lock method stays with Project admission. Zero project cycles and the existing
one module/six type cycles are unchanged.

Initial admission adds bounded Project gate acquisition before the root; ordinary runtime
updates remain root-only. This is not a capacity or fairness result. The existing static
root sequence semaphore dictionary has no removal and remains a retention concern for
the workload review. The earlier coarse Agent catalog SQL-wait comparison remains negative.
Inherited source/publication, parent outcome accounting, manager continuation, Workflow
producers, settings activation and broad/API/browser closure remain open. The historical
broad Stable result remains non-green; SPA stays on its own branch.

## Frozen evidence and resources

| Run | Source files / SHA-256 | Runtime files / SHA-256 |
| --- | --- | --- |
| Initial | 9,580 / `93656E70C5209AD0B74BA2765C6582CE36B5AACF4F4205BB96AD88847C2603E4` | 4,050 / `6D7B94742D570449E0046B674ED56855E99FE39532D3933ACFC64EE437B0D6B7` |
| Repair | 9,580 / `1626702AD5A00D04C3E2E51D9D684B0FB577BA7915FC856037009370A539C138` | 4,050 / `D0F82DFD95A402C684EFE46550ED62DF75B87919FB835144432B6E7A36B63604` |

The 584.9469351-second initial observer covers the complete 558.242292-second Integration
and 4.8182337-second Unit commands. The 120.8604205-second repair observer covers the
complete 102.0441162-second command. Both frozen identities verify unchanged before release.
Both roots recorded ownership before creation, reused PostgreSQL 18 on 53619 and the
existing 119 resource families, and left zero observed process/database-name delta.
Eight initial and four repair TEMP entries lack attributable ownership and are preserved.
The shared fixture remains required; historical cleanup denials remain unresolved.

Retained proof, with hashes of the original Windows CRLF byte form:

- [Initial run](../proof/2026-10-19-planning-authority/c4-process-project-lock-order.json), SHA-256 `987E8304E5C56F7DB864DA4FEE3DE9DABB7722466893ABC8F78BAA9FE36F0163`.
- [Repair and exact bridge](../proof/2026-10-19-planning-authority/c4-process-project-lock-order-final.json), SHA-256 `6A4687FE8ED9DE8DA1B4716F996A380244B6006148ECFC870B03AA851D3058CA`.

Git normalizes these JSON files to LF. The corresponding Git-blob SHA-256 values are
`DC47C259573777A17C8B5843489F64B029621FC7C8E7EEFFF55E8B42A27CB78F` (initial) and
`E8117C73F563E49DC93E90CF9847C48BBD1F6F6EA5CD8BA4118C606F59F1C611` (repair).
Byte comparison confirms line endings are the only difference.
