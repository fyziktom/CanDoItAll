# P05 external Agent target preconditions

Source review, 2026-10-19; implementation under validation. The policy editor checkpoint
exposed a concrete writer gap: ordinary Agent or policy edits do not advance the
external binding's cached ConfigurationVersion, and provisioning compares that
cached value instead of the actual target. Its request also re-reads the mutable
editor inside an asynchronous store callback after fingerprinting earlier values.

## Ownership and chosen change

Keep AgentExternalProvisioningService as the application owner, the existing file
catalog publication mechanism as persistence owner, and the existing HTTP routes.
Do not add a module or project edge, generic mutation coordinator, production
partial, runtime registry or UI surface. AgentConfigurationVersion.Create remains
the existing content digest used elsewhere; do not change its contract.

The external resource's opaque 64-hex version must bind a versioned hash domain,
the original workspace identity (profile, generation and scope), normalized
external identity, actual Agent ID, actual UpdatedAtUtc revision and current
configuration digest. Thus ordinary/policy writes, target recreation, scope changes
and A-to-B-to-A edits cannot silently satisfy an old expected token. GET computes
this token from the current Agent, and reports its current archive state and
revision. A binding's last-provisioning metadata is historical, not current authority.
Accepted receipts retain the exact returned version and must never be rewritten
from a current read. Existing retained idempotency replay is checked first and
keeps its historical fingerprint/receipt semantics; a new write with an old
unscoped configuration hash must read current state and explicitly resubmit.

Freeze the complete normalized editor before the first await. Deep-copy mutable
nested values; use that same captured material for fingerprint and persistence.
Validate the original request before normalization so forbidden secret/identity
fields cannot be silently discarded. Do not add new fingerprint fields that make
retained operations unreadable. Compare actual target version in the owner callback
and retain the exact current catalog's physical publication witness.

All inputs to this operation are catalog facts: Agent, binding, providers,
capabilities and retained external operations. Use the existing narrow
ISandboxWorkspaceCatalogStore callback with its final publication validator; avoid
loading/replacing execution journals. Keep the existing file/catalog lock order and
optimistic original-byte check. Recheck the captured profile generation at read and
publication boundaries. This does not create a new atomic global profile-switch
lease or claim that a preflight check alone fences profile activation.

## Compatibility, performance and limits

The route, header shape and receipt schema stay stable. The version remains opaque
64 uppercase hexadecimal digits. Old saved receipts are historical and can still
replay exactly; they are not converted into fresh write tokens. Package-import
bindings remain readable because reads derive actual target state. Configuration
no-op detection compares content digests independently of the opaque concurrency
version, preserving the original target revision when nothing changed.

Keep the existing bounded operation history in this slice and explicitly retain its
2048-operation expiration/namespace-restoration follow-up. This is not complete
durable-retention qualification. Unknown-write retries may use only the original
idempotency key and complete captured request; new keys cannot resolve uncertainty.
Postcommit directory refresh must not recapture another active profile; retain the
acknowledged receipt when the original context cannot refresh. Wider projection
repair and namespace restore semantics remain separate gates.

The existing CRM technical-agent bridge already references AgentFramework workspace
identity models. Add an overload carrying that identity, with an explicit unsupported
default for bridges that cannot honor it. The real adapter compares the returned
catalog snapshot's profile, scope and generation before any projection publication.
The current-profile facade captures identity and service together before dispatch;
the projection store retains its existing database-profile and revision checks. No
new project edge or DI service is introduced. This protects destination identity;
it does not make the catalog commit and CRM projection one transaction.

Compatibility evidence was captured before rebuilding: the previous compiled Core
assembly SHA-256 was CF0C8633FC112C8F2A6AE219E98820E0651C95B60997F4380567BD55CD384A9A.
Invoking its original fingerprint method on the checked-in normalized fixture produced
C62A2372A1CECA3AA45235745A8CC705C415E8AB6010AAB8549B5FCB4814A845. The regression
uses this fixed value and an old content-hash receipt, independently of the new hash.

The new token adds a small hash over bounded identity fields plus the already
required configuration digest. The larger benefit is excluding execution-journal
reads/serialization from external provisioning. Verify dependencies through a
store double that rejects broad workspace access and through real file-owner
integration. No model/network work is introduced inside the owner callback.

## Required proof

Use deterministic callback barriers to mutate a submitted editor after admission,
rotate profile generation before publication, and race an ordinary/policy edit.
Prove stale update and archive are rejected without Agent/binding/operation changes;
current GET permits an explicit new write; A-to-B-to-A, identity and scope changes
invalidate old tokens; delayed input changes do not alter fingerprint or payload;
historical same-key replay returns the original receipt after later changes;
no-op updates preserve target revision; and known/unknown cleanup behavior stays
distinct. Rerun existing external provisioning unit and HTTP cases, plus relevant
policy preservation and catalog publication dependencies. Discover exact counts
after standard Release builds, enforce portability without baseline writing,
inspect every intentional baseline delta, record source/runtime identity and
reconcile the existing fixture. Do not close Process/Workflow or the E2E bundle
on this writer checkpoint.
