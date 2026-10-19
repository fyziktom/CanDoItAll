# Shared material content contract

Design before implementation, following the [canonical storage checkpoint](../validation/2026-10-19-process-material-persistence.md).
The existing executor-bound snapshot cannot represent one run material shared by Agent
and Workflow assignments with different dispatch contracts. Loosening exact executor
equality would confuse data compatibility with permission/capability. The business
content schema must instead be explicit and independent.

## Contracts and compatibility

Add a neutral content schema ID/version to Runtime. Its owner is the business module;
Runtime/Persistence validate identity and bounds without interpreting business JSON or
referencing that module. An assignment separately retains its exact execution contract.
Supported adapters must check both the content schema and their own execution contract.

Retain the original `CanonicalSnapshotV1` material protocol and fingerprint framing for
historical executor-bound records. Add an explicit `SharedContentV1` material protocol,
with a distinct fingerprint domain and required content schema. New values contain
content identity, not a fabricated capture executor. Legacy and shared content are
mutually exclusive shapes; neither can be silently converted into the other.

Policy schema 3 already contains a required protocol discriminator. Shared content adds
the required content contract under that new discriminator. Existing schema 3 readers
reject its unknown protocol/field. Absent content preserves exact legacy schema 1/2 and
executor-bound schema 3 bytes. Invalid protocol/field combinations, null smuggling and
unknown schemas must fail before persistence/provider work.

Persist an explicit scope protocol and make the old executor column nullable only for
shared content. Existing rows receive the original protocol without changing payload,
capture time, fingerprint or meaning. Reads select the proper original framing. Require
the same protocol/content for every generation of a scope. New shared content cannot be
written into an old scope, and downgrade must refuse shared content/admissions before
attempting to restore the old non-null executor column.

## Ownership and test gates

The material store remains an internal collaborator of the existing root-fenced runtime
unit of work. Its shared-content check compares the immutable admitted content contract;
the legacy branch retains exact execution-contract equality. Dispatch/source/manager
authorization remains separate and unchanged. No new current-head selection, provider
callback, journal read, transaction or project reference is introduced.

Prove exact old wire/fingerprint/storage compatibility, neutral golden fingerprints,
cross-executor sibling reuse of one payload, content ID/version/protocol mismatch,
no legacy-scope conversion, corrupt/unknown persisted formats, original replay, migration
round-trip/downgrade refusal and prepared-admission immutability. Reuse existing source
profile, budget/generation and publication regression cases. Run direct builds, exact
test discovery, final portability, architecture comparison and resource reconciliation.

This refinement does not activate a governed executor. A real parent scope/budget,
inherited child journal and producer/manager wiring still precede activation.

Implementation checkpoint: [265/265 selected cases passed](../validation/2026-10-19-process-shared-content.md),
with compatibility, migration, dependency and resource evidence. Parent accounting and
actual runtime activation remain separate required work.
