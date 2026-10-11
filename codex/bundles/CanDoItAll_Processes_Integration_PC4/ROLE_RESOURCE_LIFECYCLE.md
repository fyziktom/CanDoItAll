# Role identity and owned resource lifecycle

## Failing sequence to reproduce

Seed or select a source role whose effective authored `RoleResources` has an entry under
its usage key. Make that role unreferenced so deletion is legitimately allowed. Delete it
through the normal projection client, open a fresh native scope, and import the same source
role. Current allocation sees a free RoleUsages key while `resources.Add` can encounter the
retained old resource. Also exercise whole-process import that reuses that key.

This is not proved by merely importing a resource-free role, checking a row count, or
catching the exception and returning Accepted. Assert exact command outcome, persisted
head revision/content, resource keys, projected resolved fields and durable replay result.

## Ownership rules

Role usage key, source RoleResourceKey and immutable publication identity are distinct.
The current head may own materialized resources keyed by usage; removing the usage must
handle that ownership consistently. Identify consumers before choosing removal versus
identity allocation. Do not remove a shared source template file or another role's material.

Keep immutable publication copies and operation receipt snapshots unchanged. A previous
publication referring to the removed draft role must still decode and be usable by its
retained prepared/accepted launch under existing lifecycle policy. Do not retroactively
rewrite hashes or retained JSON. Treat draft visual references owned by a deleted role
coherently so detached clones cannot resolve to an unrelated future role accidentally.

For AddRole key reuse, ensure stale resource defaults cannot leak into the new role. For
import, a genuinely occupied identity must be remapped together with every **local** semantic
reference, including bindings/decision roles and applicable placements. Do not blindly
rewrite child-definition artifact/output references: those name the child definition, not
the imported parent's renamed step. Existing PC3 import semantics otherwise remain intact.

## Regression matrix

- Delete -> independent scope -> ImportRole, and Delete -> ImportProcess, with resource-backed
  source and exact resolved fields. Verify no orphan duplicate key or unrelated replacement.
- Delete -> AddRole where a generated key can be reused; verify only the new role's intended
  data and selection, preserving late user selection and draft rules from PC2.
- Another live role, another definition/project/profile and original source templates remain
  unchanged. Referenced-role deletion is still rejected with no mutation.
- Retained publication/receipt/old prepared launch hashes remain identical across draft delete
  and new import; same operation replay allocates neither extra keys nor another revision.
- Lost response after committed delete or import reconciles using its original operation
  identity. A stale expected revision conflicts without partial dictionary mutation.
- Real browser delete/reimport with a fresh opening shows the corrected role, not just a
  green toast. Keep draft values and editor selection across unrelated tab changes.

Use the existing native commit, clone/codec and expected-version boundaries. A new database
schema or long-lived resource cache is not expected or justified by this defect.
