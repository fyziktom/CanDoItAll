# SB06 semantic invariants

State: `PASS` for implementation and focused proof.

1. `provider.candoitall-shared` describes origin and ownership. Runtime execution remains
   `ProviderKind.OpenAi`; no shared-specific agent runtime exists.
2. Workspace owns the persisted provider/source/import graph. Inner MAF receives one complete,
   connector-neutral effective profile and never queries Workspace or SharedProviders transport.
3. A shared profile materializes only when profile/import/source relationships, source identity,
   canonical endpoint, credential reference, remote snapshot, revision, cached profile, purpose,
   transport, and model capabilities agree.
4. Temporary source failures may retain a previously validated projection, but operationally
   unavailable state always disables invocation. Corrupt or never-validated state is omitted.
5. Remote capabilities are derived from the validated publication and safe intersection across its
   models; editable local flags cannot manufacture a central capability.
6. Every source-managed runtime profile carries a strict allow-list of its publication routing model
   IDs. Raw driver, image, and MAF SDK dispatch reject any other model before network I/O.
7. Personal provider profiles remain unconstrained unless they explicitly opt into a constraint;
   their client, model override, credential, and health behavior remain compatible.
8. Provider selection is explicit. Alias/model collisions do not merge identities, and an
   unavailable selected shared profile never falls back to an available personal profile.
9. Runtime source credentials are resolved with the shared-source purpose, exact allowed secret ID,
   and source consumer identity. Credential values and source secret identifiers are not projected
   into catalog metadata or public failure messages.
10. Access-context is request-scoped. It is added only to the current outbound message, an existing
    header is preserved, cached client default headers are never mutated, and no ambient context
    means no propagated header.
11. Source-managed health/runtime failures cross public and activity boundaries only as deterministic
    sanitized messages. Personal-provider diagnostics retain their established detailed behavior.
12. Caller-requested cancellation remains cancellation. It is not converted to provider failure,
    while internal timeout, transport, streaming, and disposal failures retain typed boundaries.
13. Source-managed profiles cannot invoke speech-to-text or text-to-speech because those operations
    are absent from the shared publication contract. Driver entry fails before credential/network
    access, voice options exclude the profile, and an explicit ineligible selection stays empty
    without personal fallback.
14. A committed shared-profile change refreshes the shared catalog projection after commit; stale
    projections are removed without creating a second canonical store.
15. Inner MAF dependency direction is unchanged: no reference to Workspace, SharedProviders Http,
    Web, or UI, and no Workspace-to-Http edge.
