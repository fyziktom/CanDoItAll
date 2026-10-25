# P02b typed contract and codec outline

Status: implementation outline, 2026-10-16. No implementation or validation claim.
This narrows [the P02b card](P02b-durable-results.md),
[lifecycle contract](../../../../docs/architecture/concurrent-mutations/06-operation-lifecycle-contract.md)
and [Projects cutover](../../../../docs/architecture/concurrent-mutations/07-projects-contract-cutover.md).
Their lifecycle, permission and retention gates remain mandatory.

## Ownership and concrete types

Only `DatasetNamespaceId` belongs in SharedKernel; Infrastructure owns its persisted
lifecycle. Projects.Contracts owns the following wire values. Existing P02a command,
expectation and acknowledgement shapes remain the definition payloads.

| Type | Required fields / invariant |
| --- | --- |
| `ProjectOperationEpoch` | Positive `long Value`; zero/default is rejected at every admission boundary. |
| `ProjectCallerBinding` | Exact supported version prefix plus digest; v1 is `pc1:` and 64 lowercase hexadecimal characters. It is a retained precondition, never authority. |
| `ProjectDefinitionOperation` | `DatasetNamespaceId Namespace`, `ProjectOperationEpoch Epoch`, nonempty `Guid Id`, `ProjectCallerBinding CallerBinding`. No actor, subject, claim or credential. |
| `ProjectDefinitionRequestVersion` | Positive `int Schema`, positive `int Normalization`; initial supported pair `(1, 1)`. Syntax validation does not imply version support. |
| `ProjectDefinitionCommandKind` | Closed `Create`, `Replace`, `UpdateScalars`; unsupported numeric values rejected. |
| `ProjectDefinitionRequest<TCommand>` | Nonnull operation, interpretation version and frozen command. Supported concrete commands are the three existing P02a commands; there is no arbitrary polymorphic JSON payload. |
| `ProjectDefinitionResultVersion` | Positive codec version; initial `1`. Independent of request interpretation. |
| `ProjectDefinitionLimits` | Explicit field-byte, phase/option count, raw-request, canonical-command and accepted-result budgets. Values come from validated owner options; no invented default in a DTO. |
| `ProjectDefinitionWriteContext` | Current namespace/caller binding, open epoch, admission deadline, advertised replay horizon/effective deadlines, supported request/result versions and enforced limits. Arrays must be immutable snapshots. |

Do not make the DTO metadata a credential. Every owner call independently validates
the current SQL namespace/epoch and trusted caller. The original definition expectation
stays unchanged when the client obtains an epoch immediately before its first dispatch.
Binding, request version and command are persisted with the client operation before
dispatch; changing any semantic intent requires a new operation identity.

`ProjectMutationCallerScope` is a typed server-only value in the Projects application
assembly. It consists of closed `CallerKind` and a
fixed 32-byte `ProjectCallerKey`; use an immutable validated value, not an exposed mutable
`byte[]`. Public transport DTOs never contain it. Initial kinds are
`AuthenticatedSubject`, `OpenApi`, `LocalWorkspace`, and explicit `GovernedExecution`.
The governed factory accepts an owner-encoded stable admitted execution key; it does
not derive that key from the separately bound effect/source witness. The verified issuer/subject hash
uses bounded length-prefixed UTF-8 with case preserved and excludes token IDs/expiry.
The other two kinds use distinct fixed domain-separated keys selected by trusted
composition. Authentication failure never selects the open/native kind.

`ProjectDefinitionInvocation` belongs in Projects application code. Its fields are
trusted caller scope, `ProjectDefinitionReceiptAccess`, optional existing
`ProjectMutationAuthorization`, and an optional **typed** effect binding supported by
the current adapter. It cannot be deserialized from JSON. Scope chooses the receipt
partition; receipt access controls current disclosure; source authorization controls
new effects. None substitutes for the others.

## Closed outcomes and lookup

Use a Projects-specific closed result family, generic only over the existing full or
scalar acknowledgement. Avoid `Result<nullable acknowledgement>` and success flags
with mutually inconsistent optional fields.

| Variant | Required content and meaning |
| --- | --- |
| `Accepted<TAcknowledgement>` | Operation, result version, original accepted UTC time, exact acknowledgement, replay deadline, and outer `Replayed`. Confirmed commit or validated durable receipt only. |
| `Rejected<TAcknowledgement>` | Operation when structurally available and existing typed `Error`; this attempt is known not to have produced an effect. It says nothing about a previously unconfirmed attempt. |
| `Unconfirmed<TAcknowledgement>` | Retained operation and closed recovery-reason code; commit outcome could not be confirmed. No new identity or automatic command rerun. |

Keep the original accepted timestamp/result version/acknowledgement and any advertised
receipt deadline in canonical stored bytes. `Replayed` is response metadata outside
those bytes. Replaying cannot generate a new accepted time, definition revision,
child ID, mapping or normalized value.

Lookup is a separate closed family: `FoundFull`, `FoundScalars`,
`NotObserved(Open|Draining)`, `ClosedAbsent`, `Expired`, `DatasetChanged`,
`CallerContextChanged`, `InvalidContext(UnknownFutureEpoch)`, and `DetailUnavailable` (a closed reason including permission,
missing/replaced target, or unreadable retained detail as appropriate to disclosure).
`ClosedAbsent` carries epoch closure/observation/retention timestamps and is produced
only by the guarded, drained protocol. An empty open lookup, a later rejection,
expiry or unavailable historical detail does not resolve an earlier unconfirmed
outcome. Root HTTP adapters may map those variants to existing error envelopes while
retaining this distinction in native clients.

## Owner call signatures

```csharp
Task<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>> CreateAsync(
    ProjectDefinitionRequest<CreateProjectDefinitionCommand> request,
    ProjectDefinitionInvocation invocation, CancellationToken cancellationToken);
Task<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>> ReplaceAsync(
    ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> request,
    ProjectDefinitionInvocation invocation, CancellationToken cancellationToken);
Task<ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>> UpdateScalarsAsync(
    ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand> request,
    ProjectDefinitionInvocation invocation, CancellationToken cancellationToken);
Task<Result<ProjectDefinitionWriteContext>> GetWriteContextAsync(
    ProjectDefinitionInvocation invocation, CancellationToken cancellationToken);
Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionOperation operation,
    ProjectDefinitionInvocation invocation, CancellationToken cancellationToken);
```

Names are implementation proposals; closed behavioral variants are the contract.
Keep reserved creation an internal overload with the existing typed creation context.
Contextless ordinary writers fail with protocol-required after cutover. Do not allocate
an operation ID in a service wrapper to make an old caller appear protected.

## Codec seam and immutable normalization

`ProjectDefinitionOperationCodec` belongs beside `ProjectDefinitionRules` in Projects,
not SharedKernel or Infrastructure. It is pure: no EF, clock, ambient principal,
configuration lookup, logging or authority acquisition. No interface is needed.

The current writer has private `DefinitionWrite` and `DefinitionOutcome` records
(`ProjectDefinitionWriter.cs`); expose only the smallest internal immutable normalized
intent required by writer and codec. Keep authorization separate from serializable
intent. The version-selected owner normalizer freezes scalars, ordered child inputs,
external identity and creation/source bindings once. Codec hashing and mutation consume
that same value. Do not duplicate `ProjectDefinitionRules` or normalize again during
replay, after acquiring locks, or using current data.

Concrete codec operations are `ComputeDigest(normalizedIntent, operation, callerKey)`,
`EncodeAccepted(fullPayload)` / `EncodeAccepted(scalarPayload)` and
`DecodeAccepted(version, boundedUtf8)`. Use concrete overloads and explicit UTF-8
property order/value representation, not reflection serialization of runtime types.
Store-owned byte buffers are defensively copied or encapsulated; `ReadOnlyMemory<byte>`
over a caller-owned array alone is not an immutable receipt. Decoder validates bounds,
version, command/result kind, IDs, expectation, mapping indices and required fields.
Corrupt or unsupported bytes produce explicit detail-unavailable, never a current read.

Digest includes interpretation versions, command kind, complete namespace/epoch/ID,
trusted caller kind/key and expected public binding, original definition expectation,
normalized scalar values, ordered phase/option inputs including blank slots,
external-identity intent, supplied reservation/parent/target identities and supported
effect-relevant admitted source/basis/approval binding. Server-allocated new root/child
IDs are absent. New-child input IDs remain null. Exclude correlation IDs, bearer/token
data, retry times and the source-authority CLR object.

P02a `ProjectDefinitionCollections.Snapshot` copies arrays before the owner sees them.
It is not the bounds-first factory required by P02b. The adapter factory must inspect
existing lists before copying, and HTTP must scan bounded UTF-8 tokens before creating
unbounded lists. Owner normalization uses UTC dates; the codec must reject a purported
normalized intent containing Local/Unspecified times rather than normalize with its
host timezone. Preserve initial normalizer semantics in version 1 and test conversion
before freezing the normalized intent; later changes require a new request version.

The implemented version-one hard ceilings are 1,024 phases, 1,024 options, 65,536 UTF-8
bytes per long field, 2 MiB raw input/result and 1 MiB canonical command. These preserve
the existing 1,000-option fixture. Input validation also sums UTF-8 content with a
metadata allowance before any copy; individual field limits alone are insufficient.
Retained-data preflight remains required before enforcement. Replay uses the issued
version's hard ceilings, while a potential new effect also checks current deployment
limits; operational limit changes do not change the semantic digest.

## Source-binding integration gate

Current `ProjectMutationAuthorization` contains a source authority and copied project
admissions, but no stable admitted execution identity. `ProjectAgentSourceMutationAuthority.Bind`
and `ProjectProcessExecutionMutationService.BindProjectSource` wrap richer owner
admissions behind that interface. Hashing their runtime type or serializing the lease
cannot recover a stable semantic source binding.

Ordinary HTTP/native modes support their existing access model. A governed adapter
must supply its own reviewed typed binding before it can use protected calls; it must not
silently reuse LocalWorkspace, invent a source from model JSON or retain an unprotected
writer bypass. The P04/P07 runtime card owns the eventual decision-basis/approval
binding. Preserve existing source authorization independently when that binding is
added. Root must explicitly choose whether the P02b tool adapter is included with its
real current admitted identity or returns protocol-required until its integration
card; this is a bounded integration gate, not a reason to weaken replay semantics.
Final P02b completion includes actual governed adapter integration; refusal is not the
permanent implementation.

## Pure proof required before owner wiring

Add deterministic tests in the existing Projects unit-test assembly; proposed names:

- `Operation_address_rejects_empty_ids_invalid_epoch_and_binding`.
- `Caller_binding_changes_for_dataset_kind_or_subject_but_not_token_rotation`.
- `Caller_binding_uses_supported_submitted_version`.
- `Request_and_result_versions_are_independent_and_unknown_versions_reject`.
- `Normalized_intent_is_immutable_after_source_list_mutation`.
- `Equivalent_normalized_inputs_have_identical_golden_digest`.
- `Original_expectation_slot_order_and_source_binding_change_digest`.
- `Generated_identifiers_and_retry_metadata_do_not_change_digest`.
- `Full_and_scalar_accepted_payloads_round_trip_exact_golden_bytes`.
- `Accepted_replay_metadata_does_not_change_stored_bytes`.
- `Decoder_rejects_unknown_version_invalid_mapping_or_oversize_payload`.
- `Bounds_fail_before_collection_copy_or_canonical_buffer_growth`.

The initial implementation adds 41 source-derived unit cases in
`ProjectDefinitionOperationContractTests` (17) and `ProjectDefinitionOperationCodecTests`
(24). They have not been executed by this author; the parent owns serialized build and
validation. Unit tests cannot prove caller authority, atomic commit, closure/retirement, restore,
accepted-after-cleanup-failure or lost-response recovery. Those remain the adapter,
real PostgreSQL and host tests enumerated by the P02b card. No tests ran for this outline.
