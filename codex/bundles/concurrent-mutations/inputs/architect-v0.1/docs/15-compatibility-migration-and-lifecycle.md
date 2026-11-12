# 15 · Compatibility, migration, import, restore and rollback

## Public contract is part of the safety boundary

A protected update must receive original expected content and the appropriate lifetime/namespace, whether the adapter is HTTP, a tool or an in-process caller. An absent token cannot mean last-write-wins. Create and append commands have separate rules; do not require an update token for a resource that does not exist yet.

For new HTTP conditional representations, strong ETag/If-Match and 412 have standard meanings; a deliberately mandatory missing precondition can use 428 (EXT-08/EXT-09). Existing endpoints may use other documented invalid-request/conflict mappings. Do not change unrelated owner status codes simply for uniformity. Map owner outcome categories into a documented transport contract, not vice versa.

A stamp for editable definition fields is not automatically a strong validator for an entire dashboard including progress and related parties. Either expose a dedicated edit representation or define a separate edit-stamp contract. Resolve body/header disagreement explicitly and reject contradictory preconditions. Never let a weaker body path override a failed If-Match.

## Compatibility matrix

| Consumer or retained object | Required treatment |
|---|---|
| Current UI with original stamp | Accepted conditional write and complete outcome/reconciliation information |
| Old UI/open form with no stamp | Explicit unsupported/precondition-required response; preserve local draft for user review |
| Old API client | Versioned migration guidance or disabled protected write; no server-side stamp refill |
| Existing agent/tool schema | Upgrade read + prepare + mutation binding together; incompatible proposals cannot bypass |
| Ordinary internal C# caller | Must use owner expected-state contract; nullable optional parameter cannot become bypass |
| Create/import setup | Separate explicit create/maintenance contract, not a public Force flag |
| Bulk SQL/ExecuteUpdate | Explicit expected predicate, affected-row proof and aggregate stamp participation |
| Existing approval without required basis | Readable history or explicit re-admission path; never invented provenance |
| Published immutable definition | May remain valid for admitted computation; live publication separately conditioned |
| Existing operation receipt | Preserve exact scope, result and schema/hash-domain version |
| Imported receipt/checkpoint/history | Nonexecuting provenance unless a specifically approved migration protocol re-admits it |
| Old binary after cutover | Cannot remain an active writer to protected data |

## Canonical schema and migration

SRC-15 assigns schema authority to the complete AppDbContext/migration composition and ordinary writes to owner contexts. Add the new mappings to both appropriate models and test parity. Do not call independent owner EnsureCreated/Migrate methods or add a competing schema initializer.

Initialize distinct valid local content stamps for existing live aggregates. Define how only-child existing rows are associated with their root. Validate orphan/duplicate/foreign IDs as explicit preflight findings rather than silently dropping data. Add owner receipt uniqueness/indexes only for the selected protocol. Any backfill must have a bounded maintenance plan and be tested on representative retained data.

Coordinate migration ownership with other active branches so there is one correct migration lineage. Regenerate from the actual current head; reviewed commit IDs in this package are not migration targets. Do not roll back another developer's in-flight schema change just to reproduce the evidence baseline.

## Readiness, cutover and feature flags

Observation-only diagnostics can be introduced first, but they do not protect writes. When enforcement is enabled for an owner, all its writer paths must participate. A feature flag may expose a new UI or choose a proven projection implementation; it must not allow stale writes when contention rises. A safety fallback is to pause the affected operation with a clear error, not disable concurrency/authority checks.

Drain/reject unconverted clients and background producers. Clear or retire incompatible in-memory editors/proposals. Preserve unresolved operation evidence and complete required outcome reconciliation. Deployment safety includes the API-only UI and internal automation, not only the main web process.

## Dataset epoch and restoration

Existing profile ID/lifetime rules already distinguish ordinary incarnation and target import. First identify the existing canonical profile-generation semantics. If a destructive restore can reuse old IDs, lifetimes, stamps and receipt namespaces, the activation authority needs a fresh dataset epoch or an equivalent proven fence. This is a proposal requiring P00/P01 confirmation, not a claim that the current product lacks every such mechanism.

Do not rotate the durable dataset namespace on every ordinary host restart. That would invalidate legitimate pending receipts and long-running operations needlessly. Separate process-local request generations from durable dataset identity. When a backup replaces a dataset with older history, client requests from the overwritten future must fail re-admission rather than create duplicates in the restored past.

## Import/export and transfer

Current Projects documentation describes v3 history-preserving packages that accept v2, fresh target lifetimes and nonauthorizing imported reservations (SRC-15). Preserve those contracts. Source content stamps, claims and operation IDs may be retained as provenance but do not become current target authority. New local editable state receives local stamps. A repeated import follows the established history and replacement rules, not a generic replay of source operations.

The transfer transaction must include actual target owner mappings; do not use a global direct writer just because a new receipt table was added. Define whether compact result history transfers and how its namespace is marked imported. Avoid reviving automatic compensation against an imported project with a reused public ID.

## Rollback boundaries

An additive schema deployment can sometimes be rolled back before protected writers activate, but only under the current migration contract. After new stamps, receipts, approvals or saved records are used, older binaries may be unsafe even if SQL columns remain compatible. The default is **forward repair or paused writes**, not reinstalling a last-write-wins binary.

Keep a compatibility-safe rollback build that still enforces the new conditions, or formally stop the protected owner before an exceptional rollback. Restoring a whole backup is a dataset lifecycle event requiring the new activation fence and reconciliation. Do not drop evidence tables to get an old binary running while clients may retry.

## Privacy and payload compatibility

Conflict detail and receipt lookup obey current read permissions. Avoid disclosing another user's sensitive field values just because a write conflict occurred. Use safe field labels, redaction and minimum useful diffs. IDs/hashes in logs are correlation, not permission.

Document JSON names, enum values, token formats, operation ID scope, hash schema versions, error categories and retention/expiry behavior. Update API/XML/OpenAPI/tool documentation through the actual current repository generation path. No token appears in a URL or navigation state if it carries sensitive authority; opaque content stamps alone are not bearer credentials, but that distinction must not be blurred by future extensions.
