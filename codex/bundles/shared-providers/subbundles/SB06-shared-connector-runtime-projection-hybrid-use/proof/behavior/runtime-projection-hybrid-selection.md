# SB06 runtime projection and hybrid selection

State: `PASS`.

## Effective profile

`SharedProviderRuntimeProfileMaterializer` is a pure outer-layer adapter over the canonical Workspace
profile/import/source graph. It validates relationship IDs, canonical source address and token
reference, synchronized source identity, import selection/availability, remote snapshot schema and
revision, cached profile integrity, purpose, transport, and remote model capabilities.

An accepted graph becomes one `SharedProviderEffectiveRuntimeProfile` with:

- existing `ProviderKind.OpenAi` and the central `/openai/v1` base URI;
- Responses or Chat Completions transport, or the existing image-generation purpose;
- typed source-token reference and source identity;
- public-only or explicitly approved private-network policy;
- remote capability intersection and framework-managed history policy;
- connector origin, source/publication/revision/availability tags;
- every validated publication routing model ID.

Operational outage, authorization failure, identity mismatch, unpublish, missing, retirement, and
remote-unavailable health retain a previously validated projection but make it non-invokable. A
never-synchronized, malformed, relationship-mismatched, tampered, or corrupt graph has no projection.
The pure materializer lane discovers and passes exactly 18/18 tests.

## Runtime and catalog projection

The outer mapper converts the effective profile to the existing AgentFramework `ProviderProfile`.
The snapshot loader includes profile/import/source concurrency tokens in its composite revision.
The shared post-commit observer refreshes the existing sandbox catalog after a committed source-
managed profile change and removes a stale shared projection when canonical loading returns no
profile.

Composition registers one singleton connector-neutral HTTP selector. It selects only one of the
existing hardened public, trusted-network, or private-HTTP named clients after checking shared
origin, typed credential, enabled state, network policy, endpoint scheme, and model constraint.
Invalid partial bindings throw before a default client can be used.

The 16/16 runtime-projection integration lane proves:

- real PostgreSQL graph loading, materialization, mapping, snapshotting, and catalog projection;
- composite revision changes for profile, import, and source concurrency tokens;
- typed credential dispatch with the exact shared-source secret purpose and source consumer;
- raw Chat Completions, Responses, and image requests against a deterministic local HTTP server;
- ordinary MAF SDK Chat Completions and Responses requests against the same hardened path;
- personal OpenAI default-client compatibility;
- production DI and shared connector registration;
- disabled unavailable profile retention and corrupt graph omission;
- no inner runtime reference to Workspace or SharedProviders implementation assemblies.

## Publication model binding

`ProviderModelSelectionConstraint` is constructed from all validated models in the selected remote
publication. It uses exact ordinal matching and participates in the provider configuration
fingerprint. A source-managed profile missing its constraint is invalid.

The OpenAI raw driver enforces the constraint for chat, streaming, image, and model catalog results;
`MafProviderAgentFactory` enforces it before SDK agent creation. Proof inside the frozen 16-test lane
shows that a foreign model returned by `/models` is filtered, a foreign image model is rejected with
no request, and a cross-publication MAF model is rejected with no request. The public exception text
does not disclose the provider GUID, while typed properties retain internal diagnostic identity.
Personal profiles without a constraint preserve their established model-override behavior.

## Per-request access context

The named-client handler resolves `IAccessContextReferenceAccessor` through the active
`HttpContext.RequestServices` for each outbound message. It never captures a scoped accessor in the
singleton selector and never writes `HttpClient.DefaultRequestHeaders`.

One frozen integration fact uses the same cached client for context A, context B, and an absent
context. The deterministic server observes A, B, and no header in that order, proving there is no
cross-request leakage. The models request made without ambient context also has no header. An
already-present request header is left unchanged by code-path review.

## Hybrid selection and no fallback

The 10/10 real integration lane proves one production registry can contain personal and shared
profiles without identity collapse, even when alias/model text collide. Explicit shared selection
wins over a personal default and explicit personal selection wins over a shared default.

Source outage, authoritative unpublish, retirement, and identity mismatch retain distinct disabled
profiles. Reappearance reuses import/profile identity. Selecting an unavailable shared profile
throws a typed availability failure even while a usable personal profile exists. Two independent
client databases retain their own import identity and local enabled/alias intent.

## Audio consumer fail-closed behavior

Shared publication capability mapping does not advertise speech-to-text or text-to-speech. A typed
`ProviderAudioCapabilityPolicy` therefore rejects source-managed profiles at both OpenAI audio
driver entry points before credential resolution or HTTP dispatch. The exception retains provider
identity and operation as typed properties but exposes deterministic safe public text.

The existing voice picker filters source-managed profiles while retaining eligible personal OpenAI
providers. If persisted settings explicitly reference a now-ineligible shared provider, resolution
returns empty; it does not choose the first personal provider. Automatic personal selection is used
only when no provider was previously configured. The post-audio proof passes concrete drivers 54/54,
feature matrix/voice fail-closed characterization 16/16, and agent voice regression 29/29. The frozen
runtime-projection lane also asserts the projected shared profile is audio-ineligible.

## Compatibility and invalidation

Supporting focused lanes pass for runtime snapshots 8/8, feature matrix 16/16, concrete drivers
54/54, agent voice 29/29, preparation 9/9, connector registry 3/3, catalog projection 12/12,
profile-save validation 30/30, and the SB00 runtime path 6/6. No unfiltered or broad lane ran.
